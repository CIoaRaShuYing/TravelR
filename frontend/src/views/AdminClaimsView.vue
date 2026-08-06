<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Check, Coin, Refresh, View } from '@element-plus/icons-vue'
import { api, type ApplicantOption, type ClaimListRow, type ClaimStatus, type PayoutStatus, type Project } from '../api'
import ClaimDetailDrawer from '../components/ClaimDetailDrawer.vue'

type WorkView = 'approval' | 'payout' | 'all'
type GroupBy = 'project' | 'applicant'
type GroupRow = { key: string; label: string; claimCount: number; totalAmount: number }

const activeView = ref<WorkView>('approval')
const loading = ref(false)
const applicantLoading = ref(false)
const rows = ref<ClaimListRow[]>([])
const projects = ref<Project[]>([])
const applicants = ref<ApplicantOption[]>([])
const groups = ref<GroupRow[]>([])
const groupBy = ref<GroupBy>('project')
const total = ref(0)
const summary = reactive({ claimCount: 0, totalAmount: 0 })
const filters = reactive<{ projectId: string; applicantId: string; status: '' | ClaimStatus; payoutStatus: '' | PayoutStatus; dates: string[]; page: number; pageSize: number }>({
  projectId: '', applicantId: '', status: '', payoutStatus: '', dates: [], page: 1, pageSize: 20,
})
const detailOpen = ref(false)
const detailClaimId = ref<string | null>(null)
const detailIncludesSuperseded = ref(true)
const reviewOpen = ref(false)
const reviewAction = ref<'approve' | 'reject'>('approve')
const reviewTarget = ref<ClaimListRow | null>(null)
const reviewComment = ref('')
const payoutOpen = ref(false)
const payoutTarget = ref<ClaimListRow | null>(null)
const payoutNote = ref('')

const statusLabels: Record<ClaimStatus, string> = { Draft: '草稿', Submitted: '待审批', Approved: '已批准', Rejected: '已驳回', Cancelled: '已作废' }
const payoutLabels: Record<PayoutStatus, string> = { NotApplicable: '无需发放', Pending: '待发放', Paid: '已发放' }
const workViewLabels: Record<WorkView, string> = { approval: '待审批', payout: '待发放', all: '全部报销' }
const claimStatuses: ClaimStatus[] = ['Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled']
const payoutStatuses: PayoutStatus[] = ['NotApplicable', 'Pending', 'Paid']

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) }
function statusType(status: ClaimStatus) { return status === 'Approved' ? 'success' : status === 'Rejected' ? 'danger' : status === 'Submitted' ? 'warning' : 'info' }
function claimStatusLabel(status: string) { return statusLabels[status as ClaimStatus] }
function payoutStatusLabel(status: string) { return payoutLabels[status as PayoutStatus] }

function appliedFilters() {
  return {
    projectId: filters.projectId || undefined,
    applicantId: filters.applicantId || undefined,
    status: activeView.value === 'approval' ? 'Submitted' as ClaimStatus : activeView.value === 'payout' ? 'Approved' as ClaimStatus : filters.status || undefined,
    payoutStatus: activeView.value === 'payout' ? 'Pending' as PayoutStatus : activeView.value === 'all' ? filters.payoutStatus || undefined : undefined,
    createdFrom: filters.dates[0] || undefined,
    createdTo: filters.dates[1] || undefined,
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
      api.listAdminClaims({ ...applied, page: filters.page, pageSize: filters.pageSize }),
      api.getClaimGroupSummary({ ...applied, groupBy: groupBy.value }),
    ])
    rows.value = result.items
    total.value = result.total
    summary.claimCount = result.summary.claimCount
    summary.totalAmount = result.summary.totalAmount
    groups.value = groupResult
  } catch (error) {
    ElMessage.error(api.message(error, '加载报销管理列表失败。'))
  } finally {
    loading.value = false
  }
}

function applyFilters() { filters.page = 1; load() }
function selectGroup(group: GroupRow) {
  if (groupBy.value === 'project') filters.projectId = filters.projectId === group.key ? '' : group.key
  else filters.applicantId = filters.applicantId === group.key ? '' : group.key
  applyFilters()
}
function openDetail(row: ClaimListRow) { detailClaimId.value = row.id; detailIncludesSuperseded.value = activeView.value !== 'approval'; detailOpen.value = true }
function openReview(row: ClaimListRow, action: 'approve' | 'reject') { reviewTarget.value = row; reviewAction.value = action; reviewComment.value = ''; reviewOpen.value = true }
function openPayout(row: ClaimListRow) { payoutTarget.value = row; payoutNote.value = ''; payoutOpen.value = true }

async function confirmReview() {
  const target = reviewTarget.value
  if (!target) return
  if (reviewAction.value === 'reject' && !reviewComment.value.trim()) { ElMessage.error('驳回时必须填写原因。'); return }
  loading.value = true
  try {
    await api.reviewClaim(target.id, target.currentVersionId, reviewAction.value, {
      expectedCurrentVersionId: target.currentVersionId,
      concurrencyToken: target.concurrencyToken,
      comment: reviewComment.value.trim() || undefined,
    })
    reviewOpen.value = false
    ElMessage.success(reviewAction.value === 'approve' ? '报销已批准，进入待发放。' : '报销已驳回。')
    await Promise.all([load(), loadOptions()])
  } catch (error) {
    ElMessage.error(api.message(error, '审批报销失败。'))
    if ((error as { status?: number }).status === 409) await load()
  } finally {
    loading.value = false
  }
}

async function confirmPayout() {
  const target = payoutTarget.value
  if (!target) return
  loading.value = true
  try {
    await api.confirmPayout(target.id, { expectedCurrentVersionId: target.currentVersionId, concurrencyToken: target.concurrencyToken, note: payoutNote.value.trim() || undefined })
    payoutOpen.value = false
    ElMessage.success('发放状态已确认。')
    await load()
  } catch (error) {
    ElMessage.error(api.message(error, '确认发放失败。'))
    if ((error as { status?: number }).status === 409) await load()
  } finally {
    loading.value = false
  }
}

watch(activeView, () => { filters.page = 1; load() })
watch(groupBy, () => load())
onMounted(async () => { await loadOptions(); await load() })
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">CLAIM CONTROL DESK</p><h1>报销管理</h1><p>审批当前版本，确认发放，并按项目或人员汇总全部申请。</p></div>
      <el-tooltip content="刷新报销"><el-button circle :icon="Refresh" aria-label="刷新报销" @click="load" /></el-tooltip>
    </header>

    <el-tabs v-model="activeView" class="work-tabs">
      <el-tab-pane label="待审批" name="approval" />
      <el-tab-pane label="待发放" name="payout" />
      <el-tab-pane label="全部报销" name="all" />
    </el-tabs>

    <div class="admin-claim-filters">
      <el-select v-model="filters.projectId" clearable filterable placeholder="全部项目" @change="applyFilters"><el-option v-for="project in projects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select>
      <el-select v-model="filters.applicantId" clearable filterable remote reserve-keyword :remote-method="loadApplicants" :loading="applicantLoading" placeholder="全部申请人" @change="applyFilters"><el-option v-for="applicant in applicants" :key="applicant.id" :label="`${applicant.displayName} · ${applicant.phoneNumber}`" :value="applicant.id" /></el-select>
      <el-select v-if="activeView === 'all'" v-model="filters.status" clearable placeholder="全部报销状态" @change="applyFilters"><el-option v-for="status in claimStatuses" :key="status" :label="claimStatusLabel(status)" :value="status" /></el-select>
      <el-select v-if="activeView === 'all'" v-model="filters.payoutStatus" clearable placeholder="全部发放状态" @change="applyFilters"><el-option v-for="status in payoutStatuses" :key="status" :label="payoutStatusLabel(status)" :value="status" /></el-select>
      <el-date-picker v-model="filters.dates" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="创建开始" end-placeholder="创建结束" @change="applyFilters" />
    </div>

    <div class="claim-summary-band">
      <div><span>{{ workViewLabels[activeView] }}笔数</span><strong>{{ summary.claimCount }}</strong></div>
      <div><span>当前版本金额</span><strong>{{ money(summary.totalAmount) }}</strong></div>
      <div class="summary-mode"><span>划分方式</span><el-radio-group v-model="groupBy" size="small"><el-radio-button value="project">按项目</el-radio-button><el-radio-button value="applicant">按人员</el-radio-button></el-radio-group></div>
    </div>

    <div class="group-ledger" aria-label="报销分组汇总">
      <button v-for="group in groups" :key="group.key" type="button" :class="{ active: groupBy === 'project' ? filters.projectId === group.key : filters.applicantId === group.key }" @click="selectGroup(group)">
        <span>{{ group.label }}</span><strong>{{ group.claimCount }} 笔</strong><em>{{ money(group.totalAmount) }}</em>
      </button>
      <p v-if="!loading && groups.length === 0">当前条件下没有可汇总的报销。</p>
    </div>

    <div class="table-shell desktop-table admin-claims-table" v-loading="loading">
      <el-table :data="rows" empty-text="当前工作视图没有报销。">
        <el-table-column label="申请人" min-width="150"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.applicantName }}</strong><span>{{ scope.row.applicantId.slice(0, 8) }}</span></div></template></el-table-column>
        <el-table-column label="项目" min-width="180"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.projectName }}</strong><span>{{ scope.row.projectCode }}</span></div></template></el-table-column>
        <el-table-column label="报销" min-width="205"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.claimNumber }}</strong><span>v{{ scope.row.versionNumber }} · {{ scope.row.type === 'Travel' ? '差旅' : '单据' }}</span></div></template></el-table-column>
        <el-table-column prop="description" label="说明" min-width="180" show-overflow-tooltip />
        <el-table-column label="金额" width="125" align="right"><template #default="scope">{{ money(scope.row.totalAmount) }}</template></el-table-column>
        <el-table-column label="报销状态" width="105"><template #default="scope"><el-tag :type="statusType(scope.row.status)" effect="plain">{{ claimStatusLabel(scope.row.status) }}</el-tag></template></el-table-column>
        <el-table-column label="发放状态" width="105"><template #default="scope"><el-tag :type="scope.row.payoutStatus === 'Paid' ? 'success' : scope.row.payoutStatus === 'Pending' ? 'warning' : 'info'" effect="plain">{{ payoutStatusLabel(scope.row.payoutStatus) }}</el-tag></template></el-table-column>
        <el-table-column label="更新" width="120"><template #default="scope">{{ dateTime(scope.row.updatedAt) }}</template></el-table-column>
        <el-table-column label="操作" width="154" fixed="right"><template #default="scope"><el-tooltip content="查看详情"><el-button text circle :icon="View" aria-label="查看详情" @click="openDetail(scope.row)" /></el-tooltip><template v-if="scope.row.status === 'Submitted'"><el-tooltip content="批准报销"><el-button text circle type="success" :icon="Check" aria-label="批准报销" @click="openReview(scope.row, 'approve')" /></el-tooltip><el-tooltip content="驳回报销"><el-button text circle type="danger" aria-label="驳回报销" @click="openReview(scope.row, 'reject')">驳</el-button></el-tooltip></template><el-tooltip v-if="scope.row.status === 'Approved' && scope.row.payoutStatus === 'Pending'" content="确认发放"><el-button text circle type="warning" :icon="Coin" aria-label="确认发放" @click="openPayout(scope.row)" /></el-tooltip></template></el-table-column>
      </el-table>
    </div>

    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record admin-claim-mobile">
        <div class="mobile-record__head"><div><strong>{{ item.applicantName }} · {{ item.projectName }}</strong><span>{{ item.projectCode }} · {{ item.claimNumber }} · v{{ item.versionNumber }}</span></div><strong>{{ money(item.totalAmount) }}</strong></div>
        <p>{{ item.description || '暂无报销说明' }}</p>
        <div class="claim-mobile-status"><el-tag :type="statusType(item.status)" effect="plain">{{ statusLabels[item.status] }}</el-tag><el-tag :type="item.payoutStatus === 'Paid' ? 'success' : item.payoutStatus === 'Pending' ? 'warning' : 'info'" effect="plain">{{ payoutLabels[item.payoutStatus] }}</el-tag></div>
        <div class="mobile-record__actions"><el-button :icon="View" @click="openDetail(item)">查看</el-button><template v-if="item.status === 'Submitted'"><el-button type="success" plain :icon="Check" @click="openReview(item, 'approve')">批准</el-button><el-button type="danger" plain @click="openReview(item, 'reject')">驳回</el-button></template><el-button v-if="item.status === 'Approved' && item.payoutStatus === 'Pending'" type="warning" plain :icon="Coin" @click="openPayout(item)">确认发放</el-button></div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="当前工作视图没有报销" />
    </div>
    <el-pagination v-if="total > filters.pageSize" class="pagination" v-model:current-page="filters.page" :page-size="filters.pageSize" :total="total" layout="prev, pager, next" @current-change="load" />

    <el-dialog v-model="reviewOpen" :title="reviewAction === 'approve' ? '批准报销' : '驳回报销'" width="min(500px, calc(100vw - 32px))">
      <div v-if="reviewTarget" class="dialog-subject"><strong>{{ reviewTarget.applicantName }} · {{ reviewTarget.claimNumber }}</strong><span>{{ reviewTarget.projectName }} · v{{ reviewTarget.versionNumber }} · {{ money(reviewTarget.totalAmount) }}</span></div>
      <el-form label-position="top"><el-form-item :label="reviewAction === 'reject' ? '驳回原因' : '审批意见（可选）'" :required="reviewAction === 'reject'"><el-input v-model="reviewComment" type="textarea" :rows="4" maxlength="1000" show-word-limit /></el-form-item></el-form>
      <template #footer><el-button @click="reviewOpen = false">取消</el-button><el-button :type="reviewAction === 'approve' ? 'success' : 'danger'" :loading="loading" @click="confirmReview">{{ reviewAction === 'approve' ? '确认批准' : '确认驳回' }}</el-button></template>
    </el-dialog>

    <el-dialog v-model="payoutOpen" title="确认发放" width="min(500px, calc(100vw - 32px))">
      <div v-if="payoutTarget" class="dialog-subject"><strong>{{ payoutTarget.applicantName }} · {{ payoutTarget.claimNumber }}</strong><span>{{ payoutTarget.projectName }} · {{ money(payoutTarget.totalAmount) }}</span></div>
      <el-alert title="确认后发放状态不可直接撤销，请先核对实际付款。" type="warning" :closable="false" show-icon />
      <el-form label-position="top" class="payout-form"><el-form-item label="发放备注（可选）"><el-input v-model="payoutNote" type="textarea" :rows="3" maxlength="1000" show-word-limit placeholder="例如：银行流水号或付款批次" /></el-form-item></el-form>
      <template #footer><el-button @click="payoutOpen = false">取消</el-button><el-button type="warning" :loading="loading" @click="confirmPayout">确认已发放</el-button></template>
    </el-dialog>

    <ClaimDetailDrawer v-model="detailOpen" :claim-id="detailClaimId" :include-superseded-versions="detailIncludesSuperseded" />
  </section>
</template>
