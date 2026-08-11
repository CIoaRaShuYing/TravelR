<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Check, Close, Coin, Download, Refresh, View } from '@element-plus/icons-vue'
import { api, type ApplicantOption, type ClaimListRow, type ClaimStatus, type PaymentProfile, type PayoutStatus, type Project } from '../api'
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
const paymentProfile = ref<PaymentProfile | null>(null)
const paymentProfileLoading = ref(false)
const mealReviewOpen = ref(false)
const mealReviewAction = ref<'approve' | 'reject'>('approve')
const mealReviewTarget = ref<ClaimListRow | null>(null)
const mealDailyAmount = ref<number>()
const mealReviewComment = ref('')
const mealPayoutOpen = ref(false)
const mealPayoutTarget = ref<ClaimListRow | null>(null)
const mealPayoutNote = ref('')
const exportOpen = ref(false)
const exporting = ref(false)
const exportProjectId = ref('')
const exportDates = ref<string[]>([])

const statusLabels: Record<ClaimStatus, string> = { Draft: '草稿', Submitted: '待审批', Approved: '已批准', Rejected: '已驳回', Cancelled: '已作废' }
const payoutLabels: Record<PayoutStatus, string> = { NotApplicable: '无需发放', Pending: '待发放', Paid: '已发放' }
const workViewLabels: Record<WorkView, string> = { approval: '待审批', payout: '待发放', all: '全部报销' }
const claimStatuses: ClaimStatus[] = ['Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled']
const payoutStatuses: PayoutStatus[] = ['NotApplicable', 'Pending', 'Paid']
const mealStatusLabels: Record<string, string> = { Draft: '草稿', PendingTravelReview: '等待差旅审批', PendingReview: '待餐补审批', Approved: '已批准', Rejected: '已驳回', Cancelled: '已作废' }
const mealTotalAmount = computed(() => Number(mealDailyAmount.value ?? 0) * Number(mealReviewTarget.value?.mealAllowanceDays ?? 0))

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) }
function statusType(status: ClaimStatus) { return status === 'Approved' ? 'success' : status === 'Rejected' ? 'danger' : status === 'Submitted' ? 'warning' : 'info' }
function claimStatusLabel(status: string) { return statusLabels[status as ClaimStatus] }
function payoutStatusLabel(status: string) { return payoutLabels[status as PayoutStatus] }

function appliedFilters() {
  return {
    projectId: filters.projectId || undefined,
    applicantId: filters.applicantId || undefined,
    status: activeView.value === 'all' ? filters.status || undefined : undefined,
    payoutStatus: activeView.value === 'all' ? filters.payoutStatus || undefined : undefined,
    workQueue: activeView.value === 'all' ? undefined : activeView.value,
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
async function loadPaymentProfile(row: ClaimListRow) {
  paymentProfile.value = null
  paymentProfileLoading.value = true
  try { paymentProfile.value = await api.getPaymentProfile(row.applicantId) }
  catch (error) { ElMessage.error(api.message(error, '加载收款资料失败。')) }
  finally { paymentProfileLoading.value = false }
}
function openPayout(row: ClaimListRow) { payoutTarget.value = row; payoutNote.value = ''; payoutOpen.value = true; loadPaymentProfile(row) }
function openMealReview(row: ClaimListRow, action: 'approve' | 'reject') {
  mealReviewTarget.value = row
  mealReviewAction.value = action
  mealDailyAmount.value = undefined
  mealReviewComment.value = ''
  mealReviewOpen.value = true
}
function openMealPayout(row: ClaimListRow) {
  mealPayoutTarget.value = row
  mealPayoutNote.value = ''
  mealPayoutOpen.value = true
  loadPaymentProfile(row)
}

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

async function confirmMealReview() {
  const target = mealReviewTarget.value
  if (!target?.mealAllowanceConcurrencyToken) return
  if (mealReviewAction.value === 'approve' && (!mealDailyAmount.value || mealDailyAmount.value <= 0)) { ElMessage.error('请输入大于 0 的每日餐补金额。'); return }
  if (mealReviewAction.value === 'reject' && !mealReviewComment.value.trim()) { ElMessage.error('驳回餐补时必须填写原因。'); return }
  loading.value = true
  try {
    await api.reviewMealAllowance(target.id, mealReviewAction.value, {
      expectedCurrentVersionId: target.currentVersionId,
      claimConcurrencyToken: target.concurrencyToken,
      mealConcurrencyToken: target.mealAllowanceConcurrencyToken,
      dailyAmount: mealReviewAction.value === 'approve' ? mealDailyAmount.value : undefined,
      comment: mealReviewComment.value.trim() || undefined,
    })
    mealReviewOpen.value = false
    ElMessage.success(mealReviewAction.value === 'approve' ? '餐补金额已确认并审核通过，进入待发放。' : '餐补已驳回。')
    await load()
  } catch (error) {
    ElMessage.error(api.message(error, '审核餐补失败。'))
    if ((error as { status?: number }).status === 409) await load()
  } finally { loading.value = false }
}

async function confirmMealPayout() {
  const target = mealPayoutTarget.value
  if (!target?.mealAllowanceConcurrencyToken) return
  loading.value = true
  try {
    await api.confirmMealAllowancePayout(target.id, {
      expectedCurrentVersionId: target.currentVersionId,
      claimConcurrencyToken: target.concurrencyToken,
      mealConcurrencyToken: target.mealAllowanceConcurrencyToken,
      note: mealPayoutNote.value.trim() || undefined,
    })
    mealPayoutOpen.value = false
    ElMessage.success('餐补发放状态已确认。')
    await load()
  } catch (error) {
    ElMessage.error(api.message(error, '确认餐补发放失败。'))
    if ((error as { status?: number }).status === 409) await load()
  } finally { loading.value = false }
}

async function exportClaims() {
  if (!exportProjectId.value) { ElMessage.error('请选择导出项目。'); return }
  exporting.value = true
  try {
    const result = await api.exportClaims({ projectId: exportProjectId.value, submittedFrom: exportDates.value[0], submittedTo: exportDates.value[1] })
    const url = URL.createObjectURL(result.blob)
    const link = document.createElement('a')
    link.href = url
    link.download = result.fileName
    link.click()
    URL.revokeObjectURL(url)
    exportOpen.value = false
    ElMessage.success('月度报销 Excel 与凭证压缩包已导出。')
  } catch (error) { ElMessage.error(api.message(error, '导出报销记录失败。')) }
  finally { exporting.value = false }
}

watch(activeView, () => { filters.page = 1; load() })
watch(groupBy, () => load())
onMounted(async () => { await loadOptions(); await load() })
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">CLAIM CONTROL DESK</p><h1>报销管理</h1><p>依次审核差旅与餐补，分别确认发放，并按项目导出月度记录及凭证。</p></div>
      <div class="page-actions"><el-button :icon="Download" @click="exportOpen = true">月度导出</el-button><el-tooltip content="刷新报销"><el-button circle :icon="Refresh" aria-label="刷新报销" @click="load" /></el-tooltip></div>
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
        <el-table-column label="报销发放" width="105"><template #default="scope"><el-tag :type="scope.row.payoutStatus === 'Paid' ? 'success' : scope.row.payoutStatus === 'Pending' ? 'warning' : 'info'" effect="plain">{{ payoutStatusLabel(scope.row.payoutStatus) }}</el-tag></template></el-table-column>
        <el-table-column label="餐补" min-width="185"><template #default="scope"><div v-if="scope.row.mealAllowanceStatus" class="primary-cell"><strong>{{ mealStatusLabels[scope.row.mealAllowanceStatus] }}</strong><span>{{ scope.row.mealAllowanceDays }} 天 · {{ scope.row.mealAllowanceTotalAmount == null ? '金额待核定' : money(scope.row.mealAllowanceTotalAmount) }} · {{ payoutStatusLabel(scope.row.mealAllowancePayoutStatus ?? 'NotApplicable') }}</span></div><span v-else>-</span></template></el-table-column>
        <el-table-column label="更新" width="120"><template #default="scope">{{ dateTime(scope.row.updatedAt) }}</template></el-table-column>
        <el-table-column label="操作" width="220" fixed="right"><template #default="scope"><el-tooltip content="查看详情"><el-button text circle :icon="View" aria-label="查看详情" @click="openDetail(scope.row)" /></el-tooltip><template v-if="scope.row.status === 'Submitted'"><el-tooltip content="批准报销"><el-button text circle type="success" :icon="Check" aria-label="批准报销" @click="openReview(scope.row, 'approve')" /></el-tooltip><el-tooltip content="驳回报销"><el-button text circle type="danger" :icon="Close" aria-label="驳回报销" @click="openReview(scope.row, 'reject')" /></el-tooltip></template><template v-if="scope.row.mealAllowanceStatus === 'PendingReview'"><el-tooltip content="批准餐补"><el-button text circle type="success" :icon="Check" aria-label="批准餐补" @click="openMealReview(scope.row, 'approve')" /></el-tooltip><el-tooltip content="驳回餐补"><el-button text circle type="danger" :icon="Close" aria-label="驳回餐补" @click="openMealReview(scope.row, 'reject')" /></el-tooltip></template><el-tooltip v-if="scope.row.status === 'Approved' && scope.row.payoutStatus === 'Pending'" content="确认报销发放"><el-button text circle type="warning" :icon="Coin" aria-label="确认报销发放" @click="openPayout(scope.row)" /></el-tooltip><el-tooltip v-if="scope.row.mealAllowanceStatus === 'Approved' && scope.row.mealAllowancePayoutStatus === 'Pending'" content="确认餐补发放"><el-button text circle type="primary" :icon="Coin" aria-label="确认餐补发放" @click="openMealPayout(scope.row)" /></el-tooltip></template></el-table-column>
      </el-table>
    </div>

    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record admin-claim-mobile">
        <div class="mobile-record__head"><div><strong>{{ item.applicantName }} · {{ item.projectName }}</strong><span>{{ item.projectCode }} · {{ item.claimNumber }} · v{{ item.versionNumber }}</span></div><strong>{{ money(item.totalAmount) }}</strong></div>
        <p>{{ item.description || '暂无报销说明' }}</p>
        <div class="claim-mobile-status"><el-tag :type="statusType(item.status)" effect="plain">{{ statusLabels[item.status] }}</el-tag><el-tag :type="item.payoutStatus === 'Paid' ? 'success' : item.payoutStatus === 'Pending' ? 'warning' : 'info'" effect="plain">报销{{ payoutLabels[item.payoutStatus] }}</el-tag><el-tag v-if="item.mealAllowanceStatus" effect="plain">{{ mealStatusLabels[item.mealAllowanceStatus] }}</el-tag></div>
        <p v-if="item.mealAllowanceStatus" class="mobile-record__meta">餐补 {{ item.mealAllowanceDays }} 天 · {{ item.mealAllowanceTotalAmount == null ? '金额待核定' : money(item.mealAllowanceTotalAmount) }} · {{ payoutStatusLabel(item.mealAllowancePayoutStatus ?? 'NotApplicable') }}</p>
        <div class="mobile-record__actions"><el-button :icon="View" @click="openDetail(item)">查看</el-button><template v-if="item.status === 'Submitted'"><el-button type="success" plain :icon="Check" @click="openReview(item, 'approve')">批准报销</el-button><el-button type="danger" plain :icon="Close" @click="openReview(item, 'reject')">驳回报销</el-button></template><template v-if="item.mealAllowanceStatus === 'PendingReview'"><el-button type="success" plain :icon="Check" @click="openMealReview(item, 'approve')">批准餐补</el-button><el-button type="danger" plain :icon="Close" @click="openMealReview(item, 'reject')">驳回餐补</el-button></template><el-button v-if="item.status === 'Approved' && item.payoutStatus === 'Pending'" type="warning" plain :icon="Coin" @click="openPayout(item)">报销发放</el-button><el-button v-if="item.mealAllowanceStatus === 'Approved' && item.mealAllowancePayoutStatus === 'Pending'" type="primary" plain :icon="Coin" @click="openMealPayout(item)">餐补发放</el-button></div>
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
      <div v-loading="paymentProfileLoading" class="payment-profile-check"><span>收款人</span><strong>{{ paymentProfile?.personalName || '资料未填写' }}</strong><span>银行卡号</span><strong>{{ paymentProfile?.bankCardNumber || '资料未填写' }}</strong></div>
      <el-alert title="确认后发放状态不可直接撤销，请先核对实际付款。" type="warning" :closable="false" show-icon />
      <el-form label-position="top" class="payout-form"><el-form-item label="发放备注（可选）"><el-input v-model="payoutNote" type="textarea" :rows="3" maxlength="1000" show-word-limit placeholder="例如：银行流水号或付款批次" /></el-form-item></el-form>
      <template #footer><el-button @click="payoutOpen = false">取消</el-button><el-button type="warning" :loading="loading" :disabled="paymentProfileLoading || !paymentProfile?.personalName || !paymentProfile?.bankCardNumber" @click="confirmPayout">确认已发放</el-button></template>
    </el-dialog>

    <el-dialog v-model="mealReviewOpen" :title="mealReviewAction === 'approve' ? '审核餐补金额' : '驳回餐补'" width="min(520px, calc(100vw - 32px))">
      <div v-if="mealReviewTarget" class="dialog-subject"><strong>{{ mealReviewTarget.applicantName }} · {{ mealReviewTarget.claimNumber }}</strong><span>{{ mealReviewTarget.projectName }} · {{ mealReviewTarget.mealAllowanceDays }} 天</span></div>
      <el-form label-position="top">
        <el-form-item v-if="mealReviewAction === 'approve'" label="每日发放金额" required><el-input-number v-model="mealDailyAmount" :min="0.01" :precision="2" :controls="false" placeholder="0.00" /><span class="field-suffix">元 / 天</span></el-form-item>
        <div v-if="mealReviewAction === 'approve'" class="meal-amount-confirmation"><span>计算公式</span><strong>{{ mealReviewTarget?.mealAllowanceDays ?? 0 }} 天 × {{ money(mealDailyAmount ?? 0) }}</strong><span>申请金额</span><strong>{{ money(mealTotalAmount) }}</strong></div>
        <el-form-item :label="mealReviewAction === 'reject' ? '驳回原因' : '审批意见（可选）'" :required="mealReviewAction === 'reject'"><el-input v-model="mealReviewComment" type="textarea" :rows="3" maxlength="1000" show-word-limit /></el-form-item>
      </el-form>
      <template #footer><el-button @click="mealReviewOpen = false">取消</el-button><el-button :type="mealReviewAction === 'approve' ? 'success' : 'danger'" :loading="loading" @click="confirmMealReview">{{ mealReviewAction === 'approve' ? '确认金额并审核通过' : '确认驳回餐补' }}</el-button></template>
    </el-dialog>

    <el-dialog v-model="mealPayoutOpen" title="确认餐补发放" width="min(500px, calc(100vw - 32px))">
      <div v-if="mealPayoutTarget" class="dialog-subject"><strong>{{ mealPayoutTarget.applicantName }} · {{ mealPayoutTarget.claimNumber }}</strong><span>{{ mealPayoutTarget.projectName }} · {{ mealPayoutTarget.mealAllowanceDays }} 天 · {{ money(mealPayoutTarget.mealAllowanceTotalAmount ?? 0) }}</span></div>
      <div v-loading="paymentProfileLoading" class="payment-profile-check"><span>收款人</span><strong>{{ paymentProfile?.personalName || '资料未填写' }}</strong><span>银行卡号</span><strong>{{ paymentProfile?.bankCardNumber || '资料未填写' }}</strong></div>
      <el-alert title="确认后餐补发放状态不可直接撤销，请先核对实际付款。" type="warning" :closable="false" show-icon />
      <el-form label-position="top" class="payout-form"><el-form-item label="发放备注（可选）"><el-input v-model="mealPayoutNote" type="textarea" :rows="3" maxlength="1000" show-word-limit placeholder="例如：银行流水号或付款批次" /></el-form-item></el-form>
      <template #footer><el-button @click="mealPayoutOpen = false">取消</el-button><el-button type="primary" :loading="loading" :disabled="paymentProfileLoading || !paymentProfile?.personalName || !paymentProfile?.bankCardNumber" @click="confirmMealPayout">确认餐补已发放</el-button></template>
    </el-dialog>

    <el-dialog v-model="exportOpen" title="按项目导出月度报销" width="min(520px, calc(100vw - 32px))">
      <el-form label-position="top">
        <el-form-item label="项目" required><el-select v-model="exportProjectId" filterable placeholder="选择项目"><el-option v-for="project in projects" :key="project.id" :value="project.id" :label="`${project.code} · ${project.name}`" /></el-select></el-form-item>
        <el-form-item label="费用提交日期"><el-date-picker v-model="exportDates" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="开始日期" end-placeholder="结束日期" /></el-form-item>
      </el-form>
      <el-alert title="将下载包含 Excel 和报销凭证文件夹的 ZIP；日期留空时默认导出上月 10 日至本月 10 日（含首尾当天）的已提交记录。" type="info" :closable="false" show-icon />
      <template #footer><el-button @click="exportOpen = false">取消</el-button><el-button type="primary" :icon="Download" :loading="exporting" @click="exportClaims">导出 ZIP</el-button></template>
    </el-dialog>

    <ClaimDetailDrawer v-model="detailOpen" :claim-id="detailClaimId" :include-superseded-versions="detailIncludesSuperseded" />
  </section>
</template>
