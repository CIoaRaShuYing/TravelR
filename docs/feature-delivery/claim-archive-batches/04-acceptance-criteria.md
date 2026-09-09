# 报销归档批次：验收标准

## AC-001 批次创建

Requirement: REQ-001、REQ-002、REQ-003
Acceptance criteria: 日期范围内全部结案且未归档的父报销在一个事务中绑定到新批次；餐补通过父报销显示同一批次；名称重复、空范围、未结案或并发变化返回明确错误且不产生部分批次。
Verification method: 资格规则单测、隔离 PostgreSQL 迁移与 HTTP 集成验证。
Test data: 已支付普通报销、已支付餐补、餐补驳回、报销驳回、待发放、跨日期边界和已归档记录。
Risk: 预览与确认之间数据变化。
Status: passed（隔离 PostgreSQL + HTTP）

## AC-002 管理列表与看板

Requirement: REQ-004、REQ-005
Acceptance criteria: 报销管理、报销看板和餐补看板按同一批次得到一致成员；支持未归档、已归档和具体批次筛选；按批次划分包含“未归档”分组；归档不覆盖原审批/发放状态。
Verification method: 查询规则单测、API 响应断言、桌面和移动端浏览器验收。
Test data: 两个批次、一个未归档集合、跨项目和跨人员记录。
Risk: 三个查询根不同导致口径漂移。
Status: passed（查询规则、API 口径及桌面/移动端浏览器）

## AC-003 批次 ZIP

Requirement: REQ-006
Acceptance criteria: 同一批次多次导出的成员集合一致；ZIP 含四个工作表和凭证目录；餐补独立进入汇总；跨项目字段完整；缺失凭证时整体失败。
Verification method: ZIP 结构测试、工作表 XML 断言、HTTP Content-Type/文件名验证。
Test data: 多项目、多餐补、重名凭证、空凭证和缺失文件。
Risk: 错把日期重新查询结果作为批次成员。
Status: passed（固定成员、餐补、凭证和缺失文件单测）

## AC-004 名称、权限与兼容

Requirement: REQ-007、REQ-008
Acceptance criteria: 仅管理员可创建、改名和导出；改名不改变成员；所有写操作有审计；普通用户既有接口不回归；历史数据保持未归档。
Verification method: 权限矩阵、审计查询、完整后端测试、前端类型检查和生产构建。
Test data: 管理员、普通申请人、重复名称和并发令牌。
Risk: 新字段投影遗漏导致前端兼容问题。
Status: passed（审计、回归及普通申请人访问管理员接口返回 403）
