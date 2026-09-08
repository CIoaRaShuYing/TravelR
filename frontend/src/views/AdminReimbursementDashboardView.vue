<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh, View } from '@element-plus/icons-vue'
import {
  api,
  type ApplicantOption,
  type ClaimStatus,
  type DashboardGroupRow,
  type ExpenseCategory,
  type ExpenseItemDashboardRow,
  type Project,
} from '../api'
import ClaimDetailDrawer from '../components/ClaimDetailDrawer.vue'

type GroupBy = 'project' | 'applicant' | 'category'

const loading = ref(false)
const applicantLoading = ref(false)
const rows = ref<ExpenseItemDashboardRow[]>([])
const projects = ref<Project[]>([])
const applicants = ref<ApplicantOption[]>([])
const groups = ref<DashboardGroupRow[]>([])
const groupBy = ref<GroupBy>('project')
const total = ref(0)
const summary = reactive({ expenseItemCount: 0, totalAmount: 0, pendingAmountCount: 0 })
const filters = reactive<{ category: '' | ExpenseCategory; projectId: string; applicantId: string; dates: string[]; page: number; pageSize: number }>({
  category: '', projectId: '', applicantId: '', dates: [], page: 1, pageSize: 20,
})
const detailOpen = ref(false)
const detailClaimId = ref<string | null>(null)

const categoryLabels: Record<ExpenseCategory, string> = {
  DepartureTransport: '去程交通',
  ReturnTransport: '回程交通',
  Lodging: '住宿',
  OfficeSupplies: '办公用品',
  Meal: '餐费',
  Other: '其他',
  Unspecified: '未分类',
}
const categoryOptions: ExpenseCategory[] = ['DepartureTransport', 'ReturnTransport', 'Lodging', 'OfficeSupplies', 'Meal', 'Other']
const claimStatusLabels: Record<ClaimStatus, string> = { Draft: '草稿', Submitted: '待审批', Approved: '已批准', Rejected: '已驳回', Cancelled: '已作废' }

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function date(value?: string | null) { return value ? value.replaceAll('-', '/') : '日期待填写' }
function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) }
function claimStatusType(status: ClaimStatus) { return status === 'Approved' ? 'success' : status === 'Rejected' ? 'danger' : status === 'Submitted' ? 'warning' : 'info' }
function claimStatusLabel(status: ClaimStatus) { return claimStatusLabels[status] }
function categoryLabel(category: ExpenseCategory) { return categoryLabels[category] }
function groupLabel(group: DashboardGroupRow) { return groupBy.value === 'category' ? categoryLabels[group.key as ExpenseCategory] ?? group.label : group.label }
function groupActive(group: DashboardGroupRow) {
  if (groupBy.value === 'project') return filters.projectId === group.key
  if (groupBy.value === 'applicant') return filters.applicantId === group.key
  return filters.category === group.key
}

function appliedFilters() {
  return {
    category: filters.category || undefined,
    projectId: filters.projectId || undefined,
    applicantId: filters.applicantId || undefined,
    expenseFrom: filters.dates[0] || undefined,
    expenseTo: filters.dates[1] || undefined,
  }
}

async function loadOptions() {
  try {
    const [projectResult, applicantResult] = await Promise.all([
      api.listProjects({ page: 1, pageSize: 100 }),
      api.listApplicants({ page: 1, pageSize: 100 }),
    ])
    projects.value = projectResult.items
    applicants.value = applicantResult.items
  } catch (error) {
    ElMessage.error(api.message(error, '加载筛选项失败。'))
  }
}

async function loadApplicants(keyword = '') {
  applicantLoading.value = true
  try {
    const result = await api.listApplicants({ keyword: keyword.trim() || undefined, page: 1, pageSize: 100 })
    const selected = applicants.value.find(item => item.id === filters.applicantId)
    applicants.value = selected && !result.items.some(item => item.id === selected.id) ? [selected, ...result.items] : result.items
  } catch (error) {
    ElMessage.error(api.message(error, '加载申请人目录失败。'))
  } finally {
    applicantLoading.value = false
  }
}

async function load() {
  loading.value = true
  try {
    const applied = appliedFilters()
    const [result, groupResult] = await Promise.all([
      api.listAdminExpenseItems({ ...applied, page: filters.page, pageSize: filters.pageSize }),
      api.getExpenseItemGroupSummary({ ...applied, groupBy: groupBy.value }),
    ])
    rows.value = result.items
    groups.value = groupResult
    total.value = result.total
    summary.expenseItemCount = result.summary.expenseItemCount
    summary.totalAmount = result.summary.totalAmount
    summary.pendingAmountCount = result.summary.pendingAmountCount
  } catch (error) {
    ElMessage.error(api.message(error, '加载报销看板失败。'))
  } finally {
    loading.value = false
  }
}

function applyFilters() { filters.page = 1; load() }
function selectGroup(group: DashboardGroupRow) {
  if (groupBy.value === 'project') filters.projectId = filters.projectId === group.key ? '' : group.key
  else if (groupBy.value === 'applicant') filters.applicantId = filters.applicantId === group.key ? '' : group.key
  else filters.category = filters.category === group.key ? '' : group.key as ExpenseCategory
  applyFilters()
}
function openDetail(row: ExpenseItemDashboardRow) { detailClaimId.value = row.claimId; detailOpen.value = true }

watch(groupBy, () => load())
onMounted(async () => { await loadOptions(); await load() })
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">REIMBURSEMENT ITEM BOARD</p><h1>报销看板</h1><p>逐条查看当前版本费用明细；草稿和已作废记录不纳入看板。</p></div>
      <div class="page-actions"><el-tooltip content="刷新报销看板"><el-button circle :icon="Refresh" aria-label="刷新报销看板" @click="load" /></el-tooltip></div>
    </header>

    <div class="expense-dashboard-filters">
      <el-select v-model="filters.category" clearable placeholder="全部费用类别" @change="applyFilters"><el-option v-for="category in categoryOptions" :key="category" :label="categoryLabels[category]" :value="category" /></el-select>
      <el-select v-model="filters.projectId" clearable filterable placeholder="全部项目" @change="applyFilters"><el-option v-for="project in projects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select>
      <el-select v-model="filters.applicantId" clearable filterable remote reserve-keyword :remote-method="loadApplicants" :loading="applicantLoading" placeholder="全部申请人" @change="applyFilters"><el-option v-for="applicant in applicants" :key="applicant.id" :label="`${applicant.displayName} · ${applicant.phoneNumber}`" :value="applicant.id" /></el-select>
      <el-date-picker v-model="filters.dates" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="费用开始" end-placeholder="费用结束" @change="applyFilters" />
    </div>

    <div class="dashboard-summary" aria-label="费用明细汇总">
      <div><span>费用明细</span><strong>{{ summary.expenseItemCount }} 条</strong></div>
      <div class="dashboard-summary__amount"><span>已填写金额</span><strong>{{ money(summary.totalAmount) }}</strong></div>
      <div><span>金额待填写</span><strong>{{ summary.pendingAmountCount }} 条</strong></div>
      <div class="summary-mode"><span>划分方式</span><el-radio-group v-model="groupBy" size="small"><el-radio-button value="project">按项目</el-radio-button><el-radio-button value="applicant">按人员</el-radio-button><el-radio-button value="category">按类别</el-radio-button></el-radio-group></div>
    </div>

    <div class="group-ledger" aria-label="费用明细分组汇总">
      <button v-for="group in groups" :key="group.key" type="button" :class="{ active: groupActive(group) }" @click="selectGroup(group)">
        <span>{{ groupLabel(group) }}</span><strong>{{ group.itemCount }} 条</strong><em>金额 {{ money(group.totalAmount) }}</em>
      </button>
      <p v-if="!loading && groups.length === 0">当前条件下没有可汇总的费用明细。</p>
    </div>

    <div class="table-shell desktop-table" v-loading="loading">
      <el-table :data="rows" empty-text="当前条件下没有费用明细。">
        <el-table-column label="费用类别" width="115"><template #default="scope"><el-tag effect="plain">{{ categoryLabel(scope.row.category) }}</el-tag></template></el-table-column>
        <el-table-column label="费用日期" width="115"><template #default="scope">{{ date(scope.row.expenseDate) }}</template></el-table-column>
        <el-table-column label="金额" width="125" align="right"><template #default="scope"><strong v-if="scope.row.amount != null">{{ money(scope.row.amount) }}</strong><span v-else class="amount-pending">金额待填写</span></template></el-table-column>
        <el-table-column label="申请人" min-width="140"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.applicantName }}</strong><span>{{ scope.row.applicantId.slice(0, 8) }}</span></div></template></el-table-column>
        <el-table-column label="项目" min-width="170"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.projectName }}</strong><span>{{ scope.row.projectCode }}</span></div></template></el-table-column>
        <el-table-column label="报销单" min-width="175"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.claimNumber }}</strong><span>当前版本 v{{ scope.row.versionNumber }}</span></div></template></el-table-column>
        <el-table-column prop="merchant" label="商户 / 承运方" min-width="150" show-overflow-tooltip><template #default="scope">{{ scope.row.merchant || '未填写' }}</template></el-table-column>
        <el-table-column prop="note" label="备注" min-width="160" show-overflow-tooltip><template #default="scope">{{ scope.row.note || '无' }}</template></el-table-column>
        <el-table-column label="报销状态" width="105"><template #default="scope"><el-tag :type="claimStatusType(scope.row.claimStatus)" effect="plain">{{ claimStatusLabel(scope.row.claimStatus) }}</el-tag></template></el-table-column>
        <el-table-column label="更新" width="120"><template #default="scope">{{ dateTime(scope.row.updatedAt) }}</template></el-table-column>
        <el-table-column label="操作" width="72" fixed="right"><template #default="scope"><el-tooltip content="查看报销详情"><el-button text circle :icon="View" aria-label="查看报销详情" @click="openDetail(scope.row)" /></el-tooltip></template></el-table-column>
      </el-table>
    </div>

    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record expense-dashboard-mobile">
        <div class="mobile-record__head"><div><strong>{{ categoryLabel(item.category) }} · {{ item.applicantName }}</strong><span>{{ item.projectCode }} · {{ item.claimNumber }} · v{{ item.versionNumber }}</span></div><strong>{{ item.amount == null ? '待填写' : money(item.amount) }}</strong></div>
        <p>{{ item.merchant || '商户或承运方未填写' }}{{ item.note ? ` · ${item.note}` : '' }}</p>
        <dl><div><dt>项目</dt><dd>{{ item.projectName }}</dd></div><div><dt>日期</dt><dd>{{ date(item.expenseDate) }}</dd></div><div><dt>更新</dt><dd>{{ dateTime(item.updatedAt) }}</dd></div></dl>
        <div class="claim-mobile-status"><el-tag effect="plain">{{ categoryLabel(item.category) }}</el-tag><el-tag :type="claimStatusType(item.claimStatus)" effect="plain">{{ claimStatusLabel(item.claimStatus) }}</el-tag></div>
        <div class="mobile-record__actions"><el-button :icon="View" @click="openDetail(item)">查看报销详情</el-button></div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="当前条件下没有费用明细" />
    </div>

    <el-pagination v-if="total > filters.pageSize" class="pagination" v-model:current-page="filters.page" :page-size="filters.pageSize" :total="total" layout="prev, pager, next" @current-change="load" />
    <ClaimDetailDrawer v-model="detailOpen" :claim-id="detailClaimId" :include-superseded-versions="true" />
  </section>
</template>
