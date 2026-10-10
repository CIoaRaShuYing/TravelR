<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh, View } from '@element-plus/icons-vue'
import { api, type CostCategory, type CostDashboardGroup, type CostDashboardItem, type CostDashboardOption, type CostDashboardResult, type CostTotals, type PayrollEntry } from '../api'
import ClaimDetailDrawer from '../components/ClaimDetailDrawer.vue'

const loading = ref(false)
const errorMessage = ref('')
const result = ref<CostDashboardResult | null>(null)
const users = ref<CostDashboardOption[]>([])
const projects = ref<CostDashboardOption[]>([])
const filters = reactive<{ category: '' | CostCategory; userId: string; projectId: string; groupBy: 'person' | 'project'; page: number; pageSize: number }>({
  category: '', userId: '', projectId: '', groupBy: 'person', page: 1, pageSize: 20,
})
let loadVersion = 0
let payrollVersion = 0
const claimOpen = ref(false)
const claimId = ref<string | null>(null)
const payroll = reactive({ visible: false, loading: false, month: '', row: null as PayrollEntry | null })
const categoryColumns: Array<{ category: CostCategory; label: string; paid: keyof CostTotals; pending: keyof CostTotals; total: keyof CostTotals }> = [
  { category: 'Payroll', label: '工资（实发）', paid: 'payrollPaid', pending: 'payrollPending', total: 'payrollTotal' },
  { category: 'Reimbursement', label: '报销', paid: 'reimbursementPaid', pending: 'reimbursementPending', total: 'reimbursementTotal' },
  { category: 'MealAllowance', label: '餐补', paid: 'mealAllowancePaid', pending: 'mealAllowancePending', total: 'mealAllowanceTotal' },
]
const payrollExcluded = computed(() => filters.groupBy === 'project' || !!filters.projectId)
const visibleColumns = computed(() => categoryColumns.filter(column => (!payrollExcluded.value || column.category !== 'Payroll')
  && (!filters.category || column.category === filters.category)))
const categoryLabels: Record<CostCategory, string> = { Payroll: '工资', Reimbursement: '报销', MealAllowance: '餐补' }
const incomeFields: Array<{ key: keyof PayrollEntry; label: string }> = [
  { key: 'baseSalary', label: '基本工资' }, { key: 'performanceSalary', label: '绩效工资' },
  { key: 'bonus', label: '奖金' }, { key: 'allowance', label: '津贴' }, { key: 'otherIncrease', label: '其他增加' },
]
const deductionFields: Array<{ key: keyof PayrollEntry; label: string }> = [
  { key: 'socialSecurityDeduction', label: '社保个人扣款' }, { key: 'housingFundDeduction', label: '公积金个人扣款' },
  { key: 'individualIncomeTax', label: '个人所得税' }, { key: 'otherDeduction', label: '其他扣款' },
]

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function monthLabel(value: string) { return `${value.slice(0, 4)} 年 ${value.slice(5, 7)} 月工资` }
function sourceLabel(row: CostDashboardItem) { return row.category === 'Payroll' ? monthLabel(row.payrollMonth!) : row.claimNumber }
function optionLabel(option: CostDashboardOption) { return option.description && option.description !== option.name ? `${option.name} · ${option.description}` : `${option.name} · ${option.id.slice(0, 8)}` }

async function load() {
  const version = ++loadVersion
  loading.value = true
  errorMessage.value = ''
  try {
    const response = await api.getCostDashboard({
      category: filters.category || undefined, userId: filters.userId || undefined, projectId: filters.projectId || undefined,
      groupBy: filters.groupBy, page: filters.page, pageSize: filters.pageSize,
    })
    if (version !== loadVersion) return
    result.value = response
    filters.page = response.page
  } catch (error) {
    if (version !== loadVersion) return
    result.value = null
    errorMessage.value = api.message(error, '加载总费用看板失败，请重试。')
  } finally {
    if (version === loadVersion) loading.value = false
  }
}

async function refresh() {
  await Promise.all([load(), loadOptions()])
}

async function loadOptions() {
  try {
    const options = await api.getCostDashboardOptions()
    users.value = options.users
    projects.value = options.projects
  } catch (error) {
    ElMessage.error(api.message(error, '加载费用人员和项目失败，请刷新重试。'))
  }
}

function applyFilters() { filters.page = 1; void load() }
function changeCategory() {
  if (filters.category === 'Payroll') { filters.projectId = ''; filters.groupBy = 'person' }
  applyFilters()
}
function changeGroup() {
  if (filters.groupBy === 'project' && filters.category === 'Payroll') filters.category = ''
  applyFilters()
}
function clearFilters() {
  Object.assign(filters, { category: '', userId: '', projectId: '', groupBy: 'person', page: 1 })
  void load()
}
function selectGroup(group: CostDashboardGroup) {
  if (filters.groupBy === 'person') filters.userId = filters.userId === group.id ? '' : group.id
  else filters.projectId = filters.projectId === group.id ? '' : group.id
  applyFilters()
}
function changePage(page: number) { filters.page = page; void load() }
async function openDetail(row: CostDashboardItem) {
  if (row.category !== 'Payroll') { claimId.value = row.sourceId; claimOpen.value = true; return }
  const version = ++payrollVersion
  Object.assign(payroll, { visible: true, loading: true, row: null, month: row.payrollMonth! })
  try {
    const detail = await api.getPayrollPeriod(row.sourceId)
    if (version !== payrollVersion) return
    payroll.row = detail.entries.find(entry => entry.id === row.id) ?? null
    if (!payroll.row) { payroll.visible = false; ElMessage.error('该工资明细已不存在，请刷新看板。') }
  } catch (error) {
    if (version !== payrollVersion) return
    payroll.visible = false
    ElMessage.error(api.message(error, '加载工资明细失败。'))
  } finally {
    if (version === payrollVersion) payroll.loading = false
  }
}

onMounted(refresh)
</script>

<template>
  <section class="cost-board">
    <header class="page-header">
      <div><p class="eyebrow">TOTAL COST LEDGER</p><h1>总费用看板</h1><p>汇总工资实发、报销与餐补，查看已发放和待发放费用。</p></div>
      <el-button :icon="Refresh" :loading="loading" aria-label="刷新总费用看板" @click="refresh">刷新</el-button>
    </header>

    <div class="cost-filters">
      <label><span>费用类别</span><el-select v-model="filters.category" placeholder="全部费用" aria-label="费用类别" @change="changeCategory"><el-option label="全部费用" value="" /><el-option v-for="column in categoryColumns" :key="column.category" :label="column.label" :value="column.category" /></el-select></label>
      <label><span>人员</span><el-select v-model="filters.userId" clearable filterable placeholder="全部人员" aria-label="人员" @change="applyFilters"><el-option v-for="user in users" :key="user.id" :label="optionLabel(user)" :value="user.id" /></el-select></label>
      <label><span>项目</span><el-select v-model="filters.projectId" clearable filterable :disabled="filters.category === 'Payroll'" :placeholder="filters.category === 'Payroll' ? '工资不按项目统计' : '全部项目'" aria-label="项目" @change="applyFilters"><el-option v-for="project in projects" :key="project.id" :label="`${project.description} · ${project.name}`" :value="project.id" /></el-select></label>
      <el-button @click="clearFilters">重置筛选</el-button>
    </div>
    <div class="cost-scope"><span>统计口径</span><p>{{ payrollExcluded ? '当前为项目范围，仅统计报销和餐补。' : '工资取已锁定或已完成工资表的实发金额；报销、餐补取已批准金额。' }} 总额 = 已发放 + 待发放，包含已归档费用。</p></div>

    <el-alert v-if="errorMessage" :title="errorMessage" type="error" :closable="false" show-icon><el-button text @click="refresh">重新加载</el-button></el-alert>
    <div v-loading="loading" :aria-busy="loading" class="cost-content">
      <template v-if="result">
        <div class="cost-totals" aria-label="费用汇总">
          <article class="cost-total cost-total--all"><span>{{ payrollExcluded ? '项目费用合计' : '总费用' }}</span><strong>{{ money(result.summary.totalAmount) }}</strong><dl><div><dt>已发放</dt><dd>{{ money(result.summary.paidAmount) }}</dd></div><div><dt>待发放</dt><dd>{{ money(result.summary.pendingAmount) }}</dd></div></dl></article>
          <article v-for="column in visibleColumns" :key="column.category" class="cost-total"><span>{{ column.label }}</span><strong>{{ money(result.summary[column.total]) }}</strong><dl><div><dt>已发放</dt><dd>{{ money(result.summary[column.paid]) }}</dd></div><div><dt>待发放</dt><dd>{{ money(result.summary[column.pending]) }}</dd></div></dl></article>
        </div>

        <section aria-label="费用分组统计">
          <div class="cost-section-heading"><div><h2>费用分布</h2><p>{{ result.groups.length }} 个{{ filters.groupBy === 'person' ? '人员' : '项目' }} · 点击名称查看对应费用</p></div><el-radio-group v-model="filters.groupBy" aria-label="划分方式" @change="changeGroup"><el-radio-button value="person">按人员</el-radio-button><el-radio-button value="project">按项目</el-radio-button></el-radio-group></div>
          <div class="table-shell desktop-table"><el-table :data="result.groups" row-key="id" max-height="440" empty-text="当前条件下没有已发放或待发放费用。">
            <el-table-column :label="filters.groupBy === 'person' ? '人员' : '项目'" min-width="160" fixed><template #default="{ row }"><div class="primary-cell"><el-button link type="primary" @click="selectGroup(row)">{{ row.name }}</el-button><span>{{ row.description !== row.name ? row.description : row.id.slice(0, 8) }}</span></div></template></el-table-column>
            <el-table-column v-for="column in visibleColumns" :key="column.category" :label="column.label" min-width="175" align="right"><template #default="{ row }"><div class="cost-cell"><strong>{{ money(row.totals[column.total]) }}</strong><span>已发 {{ money(row.totals[column.paid]) }}</span><span>待发 {{ money(row.totals[column.pending]) }}</span></div></template></el-table-column>
            <el-table-column label="合计" min-width="180" align="right"><template #default="{ row }"><div class="cost-cell cost-cell--total"><strong>{{ money(row.totals.totalAmount) }}</strong><span>已发 {{ money(row.totals.paidAmount) }}</span><span>待发 {{ money(row.totals.pendingAmount) }}</span></div></template></el-table-column>
          </el-table></div>
          <div class="mobile-list">
            <article v-for="group in result.groups" :key="group.id" class="mobile-record cost-group-card"><header><div><el-button link type="primary" @click="selectGroup(group)">{{ group.name }}</el-button><small>{{ group.description !== group.name ? group.description : group.id.slice(0, 8) }}</small></div><strong>{{ money(group.totals.totalAmount) }}</strong></header><div v-for="column in visibleColumns" :key="column.category" class="cost-group-line"><span>{{ column.label }}</span><div><strong>{{ money(group.totals[column.total]) }}</strong><small>已发 {{ money(group.totals[column.paid]) }} · 待发 {{ money(group.totals[column.pending]) }}</small></div></div><footer>已发放 {{ money(group.totals.paidAmount) }} · 待发放 {{ money(group.totals.pendingAmount) }}</footer></article>
            <el-empty v-if="result.groups.length === 0" description="当前条件下没有费用" />
          </div>
        </section>

        <section aria-label="费用来源明细">
          <div class="cost-section-heading"><div><h2>费用明细</h2><p>共 {{ result.total }} 条 · 汇总金额包含全部匹配记录</p></div></div>
          <div class="table-shell desktop-table"><el-table :data="result.items" empty-text="当前条件下没有费用明细。">
            <el-table-column label="类别" width="90"><template #default="{ row }"><el-tag effect="plain">{{ categoryLabels[row.category as CostCategory] }}</el-tag></template></el-table-column>
            <el-table-column label="人员" min-width="140"><template #default="{ row }"><div class="primary-cell"><strong>{{ row.userName }}</strong><span>{{ row.userDisplayName }}</span></div></template></el-table-column>
            <el-table-column label="项目" min-width="170"><template #default="{ row }"><div class="primary-cell"><strong>{{ row.projectName || '不按项目统计' }}</strong><span>{{ row.projectCode }}</span></div></template></el-table-column>
            <el-table-column label="来源" min-width="180"><template #default="{ row }">{{ sourceLabel(row) }}</template></el-table-column>
            <el-table-column label="金额" min-width="135" align="right"><template #default="{ row }"><strong class="cost-money">{{ money(row.amount) }}</strong></template></el-table-column>
            <el-table-column label="发放状态" width="110"><template #default="{ row }"><el-tag :type="row.payoutStatus === 'Paid' ? 'success' : 'warning'" effect="plain">{{ row.payoutStatus === 'Paid' ? '已发放' : '待发放' }}</el-tag></template></el-table-column>
            <el-table-column label="详情" width="70" fixed="right"><template #default="{ row }"><el-button text circle :icon="View" aria-label="查看费用详情" @click="openDetail(row)" /></template></el-table-column>
          </el-table></div>
          <div class="mobile-list"><article v-for="row in result.items" :key="`${row.category}-${row.id}`" class="mobile-record cost-detail-card"><header><div><strong>{{ row.userName }} · {{ categoryLabels[row.category] }}</strong><small>{{ sourceLabel(row) }}</small></div><strong class="cost-money">{{ money(row.amount) }}</strong></header><p>{{ row.projectName ? `${row.projectCode} · ${row.projectName}` : '工资不按项目统计' }}</p><footer><el-tag :type="row.payoutStatus === 'Paid' ? 'success' : 'warning'" effect="plain">{{ row.payoutStatus === 'Paid' ? '已发放' : '待发放' }}</el-tag><el-button :icon="View" @click="openDetail(row)">查看详情</el-button></footer></article><el-empty v-if="result.items.length === 0" description="当前条件下没有费用明细" /></div>
          <el-pagination v-if="result.total > filters.pageSize" class="pagination" :current-page="filters.page" :page-size="filters.pageSize" :total="result.total" layout="prev, pager, next" @current-change="changePage" />
        </section>
      </template>
    </div>

    <ClaimDetailDrawer v-model="claimOpen" :claim-id="claimId" />
    <el-drawer v-model="payroll.visible" title="工资费用明细" size="min(520px, 100vw)" class="payroll-detail-drawer">
      <div v-loading="payroll.loading" class="cost-payroll-detail"><template v-if="payroll.row"><div class="payroll-slip-heading"><span>{{ payroll.row.employeePersonalNameSnapshot || payroll.row.employeeDisplayNameSnapshot }} · {{ monthLabel(payroll.month) }}</span><strong>{{ money(payroll.row.netPay) }}</strong><small>实发金额 · {{ payroll.row.payoutStatus === 'Paid' ? '已发放' : '待发放' }}</small></div><section class="payroll-slip-section"><h3>收入项</h3><dl><div v-for="field in incomeFields" :key="field.key"><dt>{{ field.label }}</dt><dd>{{ money(payroll.row[field.key] as number) }}</dd></div><div class="total"><dt>应发工资</dt><dd>{{ money(payroll.row.grossPay) }}</dd></div></dl></section><section class="payroll-slip-section"><h3>扣款项</h3><dl><div v-for="field in deductionFields" :key="field.key"><dt>{{ field.label }}</dt><dd>{{ money(payroll.row[field.key] as number) }}</dd></div><div class="total"><dt>扣款合计</dt><dd>{{ money(payroll.row.totalDeductions) }}</dd></div></dl></section><section v-if="payroll.row.note" class="payroll-slip-note"><span>备注</span><p>{{ payroll.row.note }}</p></section></template></div>
    </el-drawer>
  </section>
</template>

<style scoped>
.cost-filters { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)) auto; align-items: end; gap: 16px; margin: 24px 0 14px; }
.cost-filters label { display: grid; gap: 7px; min-width: 0; }
.cost-filters label > span { color: var(--muted); font-size: 12px; }
.cost-scope { display: flex; gap: 12px; align-items: baseline; margin-bottom: 20px; font-size: 12px; line-height: 1.8; }
.cost-scope > span { flex-shrink: 0; color: var(--teal); font-weight: 700; }
.cost-scope p { margin: 0; color: var(--muted); }
.cost-content { min-height: 160px; }
.cost-totals { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 12px; }
.cost-total { min-width: 0; padding: 20px; border: 1px solid var(--line); border-top: 3px solid var(--teal); border-radius: 6px; background: var(--surface); }
.cost-total > span { color: var(--muted); font-size: 13px; }
.cost-total > strong { display: block; margin: 12px 0 20px; color: var(--spruce); font: 700 clamp(20px, 2vw, 27px) Consolas, monospace; overflow-wrap: anywhere; }
.cost-total dl { display: grid; gap: 8px; margin: 0; padding-top: 12px; border-top: 1px solid var(--line); font-size: 12px; }
.cost-total dl div { display: flex; flex-wrap: wrap; gap: 4px 10px; justify-content: space-between; }
.cost-total dt { color: var(--muted); }
.cost-total dd { margin: 0; font-family: Consolas, monospace; }
.cost-total--all { color: white; border-color: var(--spruce); border-top-color: var(--amber); background: var(--spruce); }
.cost-total--all > strong { color: white; }
.cost-total--all > span, .cost-total--all dt { color: #d7e4df; }
.cost-total--all dl { border-color: #355a52; }
.cost-section-heading { display: flex; flex-wrap: wrap; gap: 14px; align-items: center; justify-content: space-between; margin: 28px 0 14px; }
.cost-section-heading h2 { margin: 0; font-size: 18px; color: var(--spruce); }
.cost-section-heading p { margin: 6px 0 0; font-size: 12px; color: var(--muted); }
.cost-cell { display: grid; gap: 3px; font-family: Consolas, monospace; font-variant-numeric: tabular-nums; }
.cost-cell span { color: var(--muted); font-size: 11px; }
.cost-cell--total strong { color: var(--teal); }
.cost-money { font-family: Consolas, monospace; font-variant-numeric: tabular-nums; }
.cost-payroll-detail { min-height: 160px; }
.cost-group-card header, .cost-detail-card header, .cost-detail-card footer { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.cost-group-card header > div, .cost-detail-card header > div { display: grid; gap: 5px; min-width: 0; }
.cost-group-card small, .cost-detail-card small { color: var(--muted); font-size: 11px; overflow-wrap: anywhere; }
.cost-group-card header > strong, .cost-detail-card header > strong { font-family: Consolas, monospace; overflow-wrap: anywhere; text-align: right; }
.cost-group-line { display: flex; justify-content: space-between; gap: 10px; padding: 12px 0; border-bottom: 1px solid var(--line); font-size: 12px; }
.cost-group-line > div { display: grid; gap: 5px; text-align: right; }
.cost-group-line strong { font-family: Consolas, monospace; }
.cost-group-card footer { margin-top: 12px; color: var(--teal); font-size: 12px; line-height: 1.8; }
@media (max-width: 900px) { .cost-filters { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 680px) {
  .cost-filters { gap: 12px; }
  .cost-filters > .el-button { margin: 0; }
  .cost-scope { display: block; }
  .cost-totals { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .cost-total { padding: 14px 12px; }
  .cost-total > strong { font-size: 19px; margin-bottom: 14px; }
  .cost-total dl div { display: grid; }
  .cost-total--all { grid-column: 1 / -1; }
  .cost-total--all > strong { font-size: 28px; }
  .cost-total--all dl { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
</style>
