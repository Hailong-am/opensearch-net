# STJ 迁移集成测试修复进度

分支: `feature/utf8json-to-stj-migration`
目标: `./build.sh integrate 1.3.14 readonly,multinode,writable random:test_only_one` 全部通过(以及 `build.sh` 编译)。

## 背景

Utf8Json → System.Text.Json (STJ) 迁移导致 **72 个集成测试失败**(基线:1238 中 69 fail + Tests.Reproduce 3 fail)。全部是迁移回归:旧的 Utf8Json formatter 承载的行为(注入 Inferrer、stateful 请求感知反序列化、`AllowPrivate`、`[JsonFormatter]` 属性)在移植到 STJ converter 时丢失。

## 快速迭代方法(重要)

全量 `integrate` 每轮 ~2.5 分钟。单类迭代方法:
```bash
# tests/Tests.Configuration/tests.yaml (gitignored):
#   mode: i
#   opensearch_version: 1.3.14
#   test_against_already_running_opensearch: true
#   random_source_serializer: false
export OSC_YAML_FILE="$(pwd)/tests/Tests.Configuration/tests.yaml"
export OSC_INTEGRATION_TEST=1 OSC_INTEGRATION_VERSION=1.3.14
dotnet test tests/Tests/Tests.csproj -c Release -f net10.0 --filter "FullyQualifiedName~<Class>"
```
xunit 的 cluster fixture 会自行启动 ephemeral 节点;不需要单独的持久 cluster。OpenSearch 1.3.14 已缓存在 `/tmp/OpenSearchManaged/`。

**收尾时务必把 tests.yaml 还原为 unit 模式** (`mode: u`, `opensearch_version: 2.16.0`),否则默认改变。

## 根因分类(约 10 个共享根因)

### ✅ 已修复

1. **MultiGet Hits 为空** — `MultiGetResponseBuilder` 丢弃了 stateful 请求感知反序列化,`InternalHits` 从未填充。
   - 重写 `src/OpenSearch.Client/Document/Multiple/MultiGet/Request/MultiGetResponseBuilder.cs`:解析 `docs` 数组,按每个 operation 的 `ClrType` 反序列化 `MultiGetHit<T>`。
   - 覆盖:MultiGetApi/Parent/Simplified/Metadata、GetMany* (17 tests)。

2. **Get 文档 "cannot convert to Id"** — 终端 source options 缺 `IdConverterFactory`,`JoinFieldConverter.Read` 反序列化 child parent id 失败。
   - `SourceConverterFactory.GetDefaultSourceOptions` 增加 `new IdConverterFactory(s)`。

3. **MultiSearch TotalResponses=0** — 同 MultiGet 的 stateful builder 回归。
   - 重写 `src/OpenSearch.Client/Search/MultiSearch/MultiSearchResponseBuilder.cs`:解析 `took`+`responses`,按 operation `ClrType` 反序列化 `SearchResponse<T>` 并按 key 存入 `Responses`。

4. **聚合值丢失** — `AggregateConverter` 消费了 JSON 但没存到 aggregate 上。
   - ScriptedMetric:`AggregateConverter.cs` GetValueAggregate 把 value 存为 `LazyDocument` → `new ScriptedMetricAggregate(doc)`。
   - TopHits:`GetTopHitsAggregate` 把 hits 反序列化为 `List<LazyDocument>` → `new TopHitsAggregate(hits, options)`。
   - Sampler/significant_terms:`GetSingleBucketAggregate` 捕获顶层 `bg_count` 并 set 到 `BucketAggregate.BgCount`。

5. **Ping 空响应崩溃** — HEAD 请求返回空的非 seekable 流,`DefaultHighLevelSerializer` 的空流保护只处理 seekable 流,STJ 抛 "input does not contain any JSON tokens"。
   - `DefaultHighLevelSerializer` 增加 peek-一字节 的空流检测 + `PrependByteStream` helper。

6. **FieldValues.ValueOf 返回 null** — `FieldValues` 丢了 `[JsonFormatter]`,通用 IsADictionary 工厂用无参 ctor,Inferrer 未注入。
   - 新增 `src/OpenSearch.Client/CommonAbstractions/Fields/FieldValuesConverter.cs`,注册在 `IsADictionaryConverterFactory` 之前。

7. **Field-keyed 只读字典查找 "key not present"**(TermVectors/GetFieldMapping)— `IReadOnlyDictionary<Field,T>` 反序列化成普通 Field-keyed map,表达式 key 不匹配。
   - 新增 `ResolvableReadOnlyFieldDictionaryConverterFactory`,返回 `ResolvableDictionaryProxy<Field,T>`(key 经 Inferrer 解析)。

8. **GetMapping Properties 查找失败** — `Properties` 用 `IDictionary` ctor 构造,`_settings` 为 null,`Sanitize` 无法解析。
   - `IsADictionaryConverter.CreateInstance` 优先使用接受 `IConnectionSettingsValues` 的 ctor 并用 `Add`(会 sanitize)填充。

9. **TestingAnalyzers IntPtr 崩溃 + isValid 泄漏** — `ResponseBase.ApiCall`/`IsValid`/`OriginalException`/`ServerError` 的 `[IgnoreDataMember]` 只在接口上,STJ 不继承,导致 STJ 遍历到含 IntPtr 的委托图。
   - 在 `ResponseBase` 具体成员上直接加 `[IgnoreDataMember]`。

10. **Percolate 匹配 0 条** — percolator query(`QueryContainer`)作为用户文档字段,经终端 source options 序列化成 `{}`(QueryContainer 只有显式接口成员)。
    - 新增 `EmbeddedDomainTypeConverterFactory`:在终端 source options 里把 OSC domain-contract 类型委托回完整 domain options。

11. **ServerError 为 null (4xx)** — STJ 只暴露 public 属性,`ResponseBase.Error`/`StatusCode` 是 internal `[DataMember]`,被丢弃。
    - `DataMemberModifier.cs` 增加 `AddNonPublicDataMembers`:重新加入 STJ 省略的非 public `[DataMember]` 属性(带 JSON 名冲突保护,避免 ClusterHealthResponse 的 `status` 冲突)。
    - `InterfaceDataContractModifier.cs` 增加同类逻辑(处理继承的非 public `[DataMember]`,修复 `[InterfaceDataContract]` 响应如 `GetResponse<T>`)。

### 🔄 进行中 / 未解决

- **CatNodes** — `CatNodesRecord` 内部别名字段(`_b`/`_build` 等 `internal [DataMember]`)未填充,Build/Ip/Name 为 null。调查中:CatResponseBuilder 用 `Deserialize<IReadOnlyCollection<TCatRecord>>`,应走 DataMember modifier,但集合元素类型可能没触发 modifier。
- **ClusterHealthShards** — 200 响应,`Indices` 字典比较不通过(shards level)。待查。
- **cluster #4 索引 settings/模板**(agent 已给根因,尚未实现):
  - GetIndexSettings `AutoExpandReplicas` null:`DynamicIndexSettingsConverter.ReadKnownSettings` 缺 `auto_expand_replicas` 提取。
  - 模板 Settings 为空:`SetValue<T>` 提取后 `Remove` 了 key 但没重新加回 backing dictionary(应为 additive)。
  - analysis 重序列化崩溃:`ReserializeAndDeserialize` round-trip `Dictionary<string,object>` 脆弱。
  - ClusterState `ClusterName` null:`DynamicResponseBase.BackingDictionary` 从未填充(丢了 `DynamicResponseFormatter`);需新增并注册 DynamicResponse STJ converter 工厂。
- 其余散项待全量回归后确认:NodesStats(processorStats.Type)、Search Suggest/Explain、UpdateWithSource、gh2886(CommonGrams token filter)。

## 关键文件

- `src/OpenSearch.Net/Serialization/DataMemberModifier.cs` — 非 public `[DataMember]` 重加
- `src/OpenSearch.Client/CommonAbstractions/SerializationBehavior/InterfaceDataContractModifier.cs` — 同上(接口契约类型)
- `src/OpenSearch.Client/CommonAbstractions/SerializationBehavior/JsonFormatters/SourceConverterFactory.cs` — 终端 source options
- `src/OpenSearch.Client/CommonAbstractions/SerializationBehavior/OpenSearchClientSerializerOptions.cs` — converter 注册顺序
- `src/OpenSearch.Client/Aggregations/AggregateConverter.cs` — 聚合反序列化

## 进度数值 — 全部完成 ✅

- 起始:72 集成测试失败。
- **最终全量 `./build.sh integrate 1.3.14 readonly,multinode,writable random:test_only_one`:1201 passed, 0 failed, 37 skipped(SKIP 为版本门控,非失败)。integrate target Succeeded。**
- 两种 source_serializer 种子都验证:ss=false(全量 run)+ ss=true(45 个 source 相关测试全过)。
- `./build.sh build`(3236 单元测试):0 failed(修复了 MultiGet/MultiSearch builder 空流回归——单元 URL/method 测试用空响应流,builder 需 IsEmpty 保护)。

### 全部修复清单(13 类根因)

1. MultiGet builder 重写(stateful 请求感知)
2. Get "cannot convert to Id"(终端 source options 加 IdConverterFactory)
3. MultiSearch builder 重写
4. 聚合值(ScriptedMetric LazyDocument / TopHits List<LazyDocument> / Sampler bg_count)
5. Ping 空非 seekable 流(DefaultHighLevelSerializer peek + PrependByteStream)
6. FieldValues Inferrer(FieldValuesConverter)
7. Field/IndexName-keyed 只读字典(ResolvableReadOnlyDictionaryConverter,支持 Field/IndexName/RelationName)
8. GetMapping Properties(IsADictionaryConverter 优先 settings-aware ctor)
9. Analyzers IntPtr + isValid 泄漏(ResponseBase 具体成员加 [IgnoreDataMember])
10. Percolate(EmbeddedDomainTypeConverterFactory:终端 source path 委托 OSC domain 类型回 domain options)
11. ServerError null(DataMemberModifier + InterfaceDataContractModifier 重加非 public [DataMember];含 JSON 名冲突/只读别名处理)
12. cluster#4 settings:AutoExpandReplicas 提取、SetValue 改为 additive、Queries/SoftDeletes 重建、ClusterState DynamicResponseConverterFactory、FileSystemStorageImplementation
13. InlineGet(Explain/Update)`_source`:接口 getter-only 成员用具体类 internal setter + [SourceSerialization];Suggest 同理受益
14. gh2886 CommonGrams common_words 单值或数组(SingleOrManyStringConverter)
15. NodesStats KeyedProcessorStats 单键包装(KeyedProcessorStatsConverter)
16. MultiGet/MultiSearch builder 空流保护(IsEmpty,修复单元 URL/method 测试)
17. **`Source<T>` (_source 端点) 在 source_serializer=true 时用错序列化器**(种子相关)—— `SourceRequestResponseBuilder` 回归成用 built-in 高级序列化器,应使用配置的 SourceSerializer(JSON.NET)。修复:从 options 里的 `SourceConverterFactory.Settings.SourceSerializer` 解析。给 `SourceConverterFactory` 加 `internal Settings` 访问器。只在 `random:sourceserializer:true` 暴露(`SourceIntegrationTests.UseSourceSerializer`)。

18. **`--report`(CI/junit)路径下 3 个 "No test is available" 错误** —— 与序列化无关,**会话开始基线就存在**。`tests/tests.proj`(Traversal)对所有测试程序集跑 `dotnet test`;`Tests.Auth.AwsSigV4`/`EphemeralTests`/`ArtifactsApiTests` 只含 `[U]`/`[TU]` 单元测试,在集成 cluster 过滤下发现 0 个测试。旧 VSTest 桥接把空程序集当 warning(exit 0),但新的 .NET 10 `dotnet test` runner 当成 build error(`failed: 0` 但 "Build failed with 3 error(s)")。修复:`tests/.runsettings` 和 `tests/.ci.runsettings` 的 `RunConfiguration` 加 `<TreatNoTestsAsError>false</TreatNoTestsAsError>`。修复后 `./build.sh integrate ... --report` → "Build succeeded",integrate Succeeded(exit 0),0 test 失败。

### 两种 source_serializer 种子最终结果(均全绿)
- ss=false:`integrate` → 1201 passed, 0 failed(`UseSourceSerializer` SKIP)。
- ss=true:`integrate` → 1204 passed, 0 failed(`UseSourceSerializer` 运行并通过)。
- **复现 ss=true**:`./build.sh integrate 1.3.14 readonly,multinode,writable random:test_only_one random:sourceserializer:true`(命令行 build 只认 `random:`/`OSC_RANDOM_*`,不认 yaml `random_source_serializer`)。

### 收尾注意
- `tests/Tests.Configuration/tests.yaml` 已还原为 unit 模式(mode: u, 2.16.0)。
- 迭代中产生的散落 opensearch 进程需 `pkill -9 -f opensearch` 清理,否则 abstractions EphemeralClusterTests 会因端口冲突误报。

## 关于 `build.sh build` 里的 EphemeralClusterTests(环境问题,非本次改动)

`build.sh build` 结果:
- **编译 full-build:Succeeded**。
- 序列化相关单元测试:**全绿**(Tests 3235、Reproduce 95、AwsSigV4 9,0 fail)。
- 唯一失败:`OpenSearch.OpenSearch.EphemeralTests.EphemeralClusterTests` —— 位于**未改动的 `abstractions/` 包**,测试真实 OpenSearch 集群的启动/停止。

失败模式(两种,均为环境限制):
1. **非 SSL**:节点能启动(`cluster.Started` 通过),但 `SendControlC()`(第三方 ProcNet 库,依赖控制 TTY)在无头/detached 进程组环境里无法把 Ctrl-C 送达 JVM,`WaitForCompletion` 5:30 超时 → "Failed to stop node"。观察到节点在超时后不久自行退出,证实是信号投递延迟而非"杀不掉"。
2. **SSL 变体**:"cluster did not start successfully"(SSL 证书初始化在该环境失败)。

证据表明与本次序列化改动无关:
- `git status` 确认 `abstractions/` **零改动**;全部改动在 `src/OpenSearch.Client` / `src/OpenSearch.Net`。
- 会话开始时的基线 `build.sh build`(通过 Bash 工具默认执行、进程组/TTY 完整)这 4 个 EphemeralClusterTests **通过**(每个 10-20s)。之后用 nohup/setsid **detach 进程组**运行,才破坏了 ProcNet 的 Ctrl-C 投递。
- **`./build.sh integrate`(目标命令本身)不运行 EphemeralClusterTests**(integrate 模式下该 assembly TOTAL: 0),已全绿通过。

结论:序列化迁移的所有回归已修复。EphemeralClusterTests 需要有控制 TTY 且干净进程组的环境(如 CI 或交互式终端)才能通过。
