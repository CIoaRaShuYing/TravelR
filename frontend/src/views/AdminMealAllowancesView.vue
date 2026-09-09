<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh, View } from '@element-plus/icons-vue'
import { api, type ApplicantOption, type ArchiveState, type ClaimArchiveBatchSummary, type DashboardGroupRow, type MealAllowanceListRow, type MealAllowanceStatus, type PayoutStatus, type Project } from '../api'
import ClaimDetailDrawer from '../components/ClaimDetailDrawer.vue'

const loading = ref(false)
const applicantLoading = ref(false)
const rows = ref<MealAllowanceListRow[]>([])
const projects = ref<Project[]>([])
const applicants = ref<ApplicantOption[]>([])
const groups = ref<DashboardGroupRow[]>([])
const archiveBatches = ref<ClaimArchiveBatchSummary[]>([])
const groupBy = ref<'project' | 'applicant' | 'archiveBatch'>('project')
const total = ref(0)
const summary = reactive({ mealAllowanceCount: 0, determinedAmount: 0, pendingAmountCount: 0 })
const filters = reactive<{ projectId: string; applicantId: string; archiveState: ArchiveState; archiveBatchId: string; dates: string[]; page: number; pageSize: number }>({
  projectId: '', applicantId: '', archiveState: 'all', archiveBatchId: '', dates: [], page: 1, pageSize: 20,
})
const detailOpen = ref(false)
const detailClaimId = ref<string | null>(null)

const mealStatusLabels: Record<MealAllowanceStatus, string> = {
  Draft: '草稿',
  PendingTravelReview: '等待差旅审批',
  PendingReview: '待餐补审批',
  Approved: '已批准',
  Rejected: '已驳回',
  Cancelled: '已作废',
}
const payoutLabels: Record<PayoutStatus, string> = { NotApplicable: '无需发放', Pending: '待发放', Paid: '已发放' }

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function date(value?: string | null) { return value ? value.replaceAll('-', '/') : '日期待补充' }
function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) }
function mealStatusType(status: MealAllowanceStatus) { return status === 'Approved' ? 'success' : status === 'Rejected' ? 'danger' : status === 'PendingReview' || status === 'PendingTravelReview' ? 'warning' : 'info' }
function payoutStatusType(status: PayoutStatus) { return status === 'Paid' ? 'success' : status === 'Pending' ? 'warning' : 'info' }
function mealStatusLabel(status: MealAllowanceStatus) { return mealStatusLabels[status] }
function payoutStatusLabel(status: PayoutStatus) { return payoutLabels[status] }

function appliedFilters() {
  return {
    projectId: filters.projectId || undefined,
    applicantId: filters.applicantId || undefined,
    tripFrom: filters.dates[0] || undefined,
    tripTo: filters.dates[1] || undefined,
    archiveState: filters.archiveState,
    archiveBatchId: filters.archiveBatchId || undefined,
  }
}

async function loadOptions() {
  try {
    const [projectResult, applicantResult, batchResult] = await Promise.all([
      api.listProjects({ page: 1, pageSize: 100 }),
      api.listApplicants({ page: 1, pageSize: 100 }),
      api.listClaimArchiveBatches(),
    ])
    projects.value = projectResult.items
    applicants.value = applicantResult.items
    archiveBatches.value = batchResult
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
      api.listAdminMealAllowances({ ...applied, page: filters.page, pageSize: filters.pageSize }),
      api.getMealAllowanceGroupSummary({ ...applied, groupBy: groupBy.value }),
    ])
    rows.value = result.items
    groups.value = groupResult
    total.value = result.total
    summary.mealAllowanceCount = result.summary.mealAllowanceCount
    summary.determinedAmount = result.summary.determinedAmount
    summary.pendingAmountCount = result.summary.pendingAmountCount
  } catch (error) {
    ElMessage.error(api.message(error, '加载餐补看板失败。'))
  } finally {
    loading.value = false
  }
}

function applyFilters() { filters.page = 1; load() }
function selectGroup(group: DashboardGroupRow) {
  if (groupBy.value === 'project') filters.projectId = filters.projectId === group.key ? '' : group.key ?? ''
  else if (groupBy.value === 'applicant') filters.applicantId = filters.applicantId === group.key ? '' : group.key ?? ''
  else if (group.key) {
    filters.archiveBatchId = filters.archiveBatchId === group.key ? '' : group.key
    filters.archiveState = 'all'
  } else {
    filters.archiveBatchId = ''
    filters.archiveState = filters.archiveState === 'unarchived' ? 'all' : 'unarchived'
  }
  applyFilters()
}
function groupActive(group: DashboardGroupRow) {
  if (groupBy.value === 'project') return filters.projectId === group.key
  if (groupBy.value === 'applicant') return filters.applicantId === group.key
  return group.key ? filters.archiveBatchId === group.key : filters.archiveState === 'unarchived' && !filters.archiveBatchId
}
function openDetail(row: MealAllowanceListRow) { detailClaimId.value = row.claimId; detailOpen.value = true }

watch(groupBy, () => load())
onMounted(async () => { await loadOptions(); await load() })
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">MEAL ALLOWANCE BOARD</p><h1>餐补看板</h1><p>集中查看各报销当前版本的餐补，按行程时间、项目和人员快速筛选。</p></div>
      <div class="page-actions"><el-tooltip content="刷新餐补"><el-button circle :icon="Refresh" aria-label="刷新餐补" @click="load" /></el-tooltip></div>
    </header>

    <div class="meal-allowance-filters">
      <el-select v-model="filters.projectId" clearable filterable placeholder="全部项目" @change="applyFilters"><el-option v-for="project in projects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select>
      <el-select v-model="filters.applicantId" clearable filterable remote reserve-keyword :remote-method="loadApplicants" :loading="applicantLoading" placeholder="全部申请人" @change="applyFilters"><el-option v-for="applicant in applicants" :key="applicant.id" :label="`${applicant.displayName} · ${applicant.phoneNumber}`" :value="applicant.id" /></el-select>
      <el-select v-model="filters.archiveState" placeholder="全部归档状态" @change="filters.archiveBatchId = ''; applyFilters()"><el-option label="全部归档状态" value="all" /><el-option label="已归档" value="archived" /><el-option label="未归档" value="unarchived" /></el-select>
      <el-select v-model="filters.archiveBatchId" clearable filterable placeholder="全部归档批次" @change="filters.archiveState = 'all'; applyFilters()"><el-option v-for="batch in archiveBatches" :key="batch.id" :label="batch.name" :value="batch.id" /></el-select>
      <el-date-picker v-model="filters.dates" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="行程开始" end-placeholder="行程结束" @change="applyFilters" />
    </div>

    <div class="dashboard-summary" aria-label="餐补汇总">
      <div><span>餐补笔数</span><strong>{{ summary.mealAllowanceCount }}</strong></div>
      <div class="dashboard-summary__amount"><span>已核定金额</span><strong>{{ money(summary.determinedAmount) }}</strong></div>
      <div><span>金额待核定</span><strong>{{ summary.pendingAmountCount }} 笔</strong></div>
      <div class="summary-mode"><span>划分方式</span><el-radio-group v-model="groupBy" size="small"><el-radio-button value="project">按项目</el-radio-button><el-radio-button value="applicant">按人员</el-radio-button><el-radio-button value="archiveBatch">按归档</el-radio-button></el-radio-group></div>
    </div>

    <div class="group-ledger" aria-label="餐补分组汇总">
      <button v-for="group in groups" :key="group.key ?? 'unarchived'" type="button" :class="{ active: groupActive(group) }" @click="selectGroup(group)">
        <span>{{ group.label }}</span><strong>{{ group.itemCount }} 笔</strong><em>已核定 {{ money(group.totalAmount) }}</em>
      </button>
      <p v-if="!loading && groups.length === 0">当前条件下没有可汇总的餐补。</p>
    </div>

    <div class="table-shell desktop-table" v-loading="loading">
      <el-table :data="rows" empty-text="当前条件下没有餐补。">
        <el-table-column label="申请人" min-width="140"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.applicantName }}</strong><span>{{ scope.row.applicantId.slice(0, 8) }}</span></div></template></el-table-column>
        <el-table-column label="项目" min-width="180"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.projectName }}</strong><span>{{ scope.row.projectCode }}</span></div></template></el-table-column>
        <el-table-column label="报销单" min-width="180"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.claimNumber }}</strong><span>当前版本 v{{ scope.row.versionNumber }}</span></div></template></el-table-column>
        <el-table-column label="行程日期" min-width="190"><template #default="scope">{{ date(scope.row.departureDate) }} — {{ date(scope.row.returnDate) }}</template></el-table-column>
        <el-table-column prop="days" label="天数" width="75" align="right" />
        <el-table-column label="每日金额" width="115" align="right"><template #default="scope"><span v-if="scope.row.dailyAmount != null">{{ money(scope.row.dailyAmount) }}</span><span v-else class="amount-pending">待核定</span></template></el-table-column>
        <el-table-column label="餐补总额" width="125" align="right"><template #default="scope"><strong v-if="scope.row.totalAmount != null">{{ money(scope.row.totalAmount) }}</strong><span v-else class="amount-pending">金额待核定</span></template></el-table-column>
        <el-table-column label="餐补状态" width="125"><template #default="scope"><el-tag :type="mealStatusType(scope.row.status)" effect="plain">{{ mealStatusLabel(scope.row.status) }}</el-tag></template></el-table-column>
        <el-table-column label="发放状态" width="105"><template #default="scope"><el-tag :type="payoutStatusType(scope.row.payoutStatus)" effect="plain">{{ payoutStatusLabel(scope.row.payoutStatus) }}</el-tag></template></el-table-column>
        <el-table-column label="归档" min-width="150"><template #default="scope"><el-tag v-if="scope.row.archiveBatchId" type="info" effect="plain">{{ scope.row.archiveBatchName }}</el-tag><span v-else class="amount-pending">未归档</span></template></el-table-column>
        <el-table-column label="更新" width="120"><template #default="scope">{{ dateTime(scope.row.updatedAt) }}</template></el-table-column>
        <el-table-column label="操作" width="72" fixed="right"><template #default="scope"><el-tooltip content="查看报销详情"><el-button text circle :icon="View" aria-label="查看报销详情" @click="openDetail(scope.row)" /></el-tooltip></template></el-table-column>
      </el-table>
    </div>

    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record meal-allowance-mobile">
        <div class="mobile-record__head"><div><strong>{{ item.applicantName }} · {{ item.projectName }}</strong><span>{{ item.projectCode }} · {{ item.claimNumber }} · v{{ item.versionNumber }}</span></div><strong>{{ item.totalAmount == null ? '待核定' : money(item.totalAmount) }}</strong></div>
        <dl><div><dt>行程</dt><dd>{{ date(item.departureDate) }} — {{ date(item.returnDate) }}</dd></div><div><dt>餐补</dt><dd>{{ item.days }} 天 · 每日 {{ item.dailyAmount == null ? '待核定' : money(item.dailyAmount) }}</dd></div><div><dt>更新</dt><dd>{{ dateTime(item.updatedAt) }}</dd></div></dl>
        <div class="claim-mobile-status"><el-tag :type="mealStatusType(item.status)" effect="plain">{{ mealStatusLabels[item.status] }}</el-tag><el-tag :type="payoutStatusType(item.payoutStatus)" effect="plain">{{ payoutLabels[item.payoutStatus] }}</el-tag><el-tag v-if="item.archiveBatchId" type="info" effect="plain">{{ item.archiveBatchName }}</el-tag></div>
        <div class="mobile-record__actions"><el-button :icon="View" @click="openDetail(item)">查看报销详情</el-button></div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="当前条件下没有餐补" />
    </div>

    <el-pagination v-if="total > filters.pageSize" class="pagination" v-model:current-page="filters.page" :page-size="filters.pageSize" :total="total" layout="prev, pager, next" @current-change="load" />
    <ClaimDetailDrawer v-model="detailOpen" :claim-id="detailClaimId" :include-superseded-versions="true" />
  </section>
</template>
