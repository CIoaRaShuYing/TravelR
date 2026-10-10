# 总费用看板：技术方案

- 新增 `CostDashboardService.cs`，统一查询三类只读行。按用户或项目聚合六个金额（每类已发、待发），并据分组结果生成全局合计。明细数据库分页，稳定排序。
- API：`GET /api/admin/cost-dashboard`，参数 category、userId、projectId、groupBy(person/project)、page、pageSize；返回 summary、groups、items、total、page、pageSize、payrollExcluded。
- API：`GET /api/admin/cost-dashboard/options`，返回存在有效费用的全部人员和项目（包含已停用对象）。两个接口沿用 Administrator 权限及 `no-store, private`。
- 不新增实体、表、索引、迁移或依赖；回滚为撤销新增服务、路由、页面和 API 封装，无数据恢复步骤。
- 前端 `/admin/cost-dashboard`：四个分类汇总卡同时呈现已发/待发；人员/项目筛选；按人员/项目统计表；分页来源明细；移动卡片；错误重试和空结果。
- 项目模式隐藏工资汇总及工资分组列，显示统计范围说明；选择工资类别回到人员模式并清除项目条件。
- 沿用深绿 `#183a34`、青绿 `#1b6b5a`、琥珀 `#d99614`、灰绿 `#eef2ef`、白色 `#ffffff` 主题，中文正文沿用系统字体，金额用 Consolas 等宽数字。以每类“已发 + 待发 = 合计”作为页面视觉主线，不新增图表依赖。
- 验证：业务筛选/归并/金额测试，Npgsql SQL 翻译测试，后端构建和测试，前端 production build；环境允许时隔离 PostgreSQL API 与桌面/移动端 smoke。
- 不采用：工资项目分摊、日期统计、把全部明细拉到浏览器求和、按姓名合并人员、直接复用旧报销看板总额。
