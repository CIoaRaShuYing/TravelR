<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Download, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { api, type ApiProblem, type PayrollCandidate, type PayrollEntry, type PayrollEntryInput, type PayrollPeriodDetail, type PayrollPeriodStatus, type PayoutStatus } from '../api'

const loading = ref(false)
const periods = ref<PayrollPeriodDetail['period'][]>([])
const selectedPeriodId = ref('')
const detail = ref<PayrollPeriodDetail | null>(null)
const keyword = ref('')
const payoutFilter = ref<'' | PayoutStatus>('')
const originals = new Map<string, string>()
const createDialog = reactive({ visible: false, month: currentChinaMonth() })
const addDialog = reactive({ visible: false, loading: false, keyword: '', candidates: [] as PayrollCandidate[], selected: [] as string[] })
const editDialog = reactive({ visible: false, row: null as PayrollEntry | null })
const payoutDialog = reactive({ visible: false, row: null as PayrollEntry | null, note: '' })

const period = computed(() => detail.value?.period ?? null)
const isDraft = computed(() => period.value?.status === 'Draft')
const dirtyRows = computed(() => detail.value?.entries.filter(row => originals.get(row.id) !== editableSnapshot(row)) ?? [])
const filteredRows = computed(() => (detail.value?.entries ?? []).filter(row => {
  const name = `${row.employeePersonalNameSnapshot ?? ''} ${row.employeeDisplayNameSnapshot}`.toLowerCase()
  return (!keyword.value.trim() || name.includes(keyword.value.trim().toLowerCase())) && (!payoutFilter.value || row.payoutStatus === payoutFilter.value)
}))

const statusLabels: Record<PayrollPeriodStatus, string> = { Draft: '草稿', ReadyForPayout: '待发放', Completed: '已完成', Cancelled: '已取消' }
const payoutLabels: Record<PayoutStatus, string> = { NotApplicable: '未锁定', Pending: '待发放', Paid: '已发放' }
const payoutOptions: Array<{ value: PayoutStatus; label: string }> = [
  { value: 'NotApplicable', label: '未锁定' },
  { value: 'Pending', label: '待发放' },
  { value: 'Paid', label: '已发放' },
]

function currentChinaMonth() {
  const parts = new Intl.DateTimeFormat('zh-CN', { timeZone: 'Asia/Shanghai', year: 'numeric', month: '2-digit' }).formatToParts(new Date())
  return `${parts.find(part => part.type === 'year')?.value}-${parts.find(part => part.type === 'month')?.value}`
}
function monthLabel(value: string) { return `${value.slice(0, 4)} 年 ${value.slice(5, 7)} 月` }
function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function dateTime(value?: string | null) { return value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '—' }
function employeeName(row: PayrollEntry) { return row.employeePersonalNameSnapshot || row.employeeDisplayNameSnapshot }
function statusType(status: PayrollPeriodStatus) { return status === 'Completed' ? 'success' : status === 'ReadyForPayout' ? 'warning' : status === 'Cancelled' ? 'info' : 'primary' }
function payoutType(status: PayoutStatus) { return status === 'Paid' ? 'success' : status === 'Pending' ? 'warning' : 'info' }
function payoutLabel(status: PayoutStatus) { return payoutLabels[status] }
function gross(row: PayrollEntry) { return row.baseSalary + row.performanceSalary + row.bonus + row.allowance + row.otherIncrease }
function deductions(row: PayrollEntry) { return row.socialSecurityDeduction + row.housingFundDeduction + row.individualIncomeTax + row.otherDeduction }
function net(row: PayrollEntry) { return gross(row) - deductions(row) }
function editableSnapshot(row: PayrollEntry) {
  return JSON.stringify([row.baseSalary, row.performanceSalary, row.bonus, row.allowance, row.otherIncrease, row.socialSecurityDeduction, row.housingFundDeduction, row.individualIncomeTax, row.otherDeduction, row.note ?? ''])
}
function setDetail(value: PayrollPeriodDetail) {
  detail.value = { period: { ...value.period }, entries: value.entries.map(row => ({ ...row, payoutRecord: row.payoutRecord ? { ...row.payoutRecord } : null })) }
  originals.clear()
  detail.value.entries.forEach(row => originals.set(row.id, editableSnapshot(row)))
  const index = periods.value.findIndex(item => item.id === value.period.id)
  if (index >= 0) periods.value[index] = { ...value.period }
  else periods.value.unshift({ ...value.period })
}

async function loadPeriods(preferredId?: string) {
  periods.value = await api.listPayrollPeriods()
  const currentActive = periods.value.find(item => item.payrollMonth.startsWith(currentChinaMonth()) && item.status !== 'Cancelled')
  selectedPeriodId.value = preferredId && periods.value.some(item => item.id === preferredId) ? preferredId : currentActive?.id ?? periods.value[0]?.id ?? ''
  if (selectedPeriodId.value) await loadDetail()
  else detail.value = null
}
async function loadDetail() {
  if (!selectedPeriodId.value) return
  if (dirtyRows.value.length && detail.value) {
    try { await ElMessageBox.confirm('当前有未保存的工资修改，刷新后将丢失。仍要继续吗？', '刷新工资表', { type: 'warning' }) }
    catch {
      selectedPeriodId.value = detail.value.period.id
      return
    }
  }
  loading.value = true
  try { setDetail(await api.getPayrollPeriod(selectedPeriodId.value)) }
  catch (error) { ElMessage.error(api.message(error, '加载工资表失败。')) }
  finally { loading.value = false }
}
async function createPeriod() {
  if (!createDialog.month) return ElMessage.warning('请选择工资月份。')
  loading.value = true
  try {
    const result = await api.createPayrollPeriod(`${createDialog.month}-01`)
    createDialog.visible = false
    await loadPeriods(result.period.id)
    ElMessage.success('工资表已创建。')
  } catch (error) { ElMessage.error(api.message(error, '创建工资表失败。')) }
  finally { loading.value = false }
}
async function loadCandidates() {
  if (!period.value) return
  addDialog.loading = true
  try { addDialog.candidates = await api.listPayrollCandidates(period.value.id, addDialog.keyword.trim() || undefined) }
  catch (error) { ElMessage.error(api.message(error, '加载候选人员失败。')) }
  finally { addDialog.loading = false }
}
async function openAdd() {
  addDialog.visible = true
  addDialog.selected = []
  addDialog.keyword = ''
  await loadCandidates()
}
async function addEntries() {
  if (!period.value || !addDialog.selected.length) return ElMessage.warning('请选择人员。')
  addDialog.loading = true
  try {
    setDetail(await api.addPayrollEntries(period.value.id, { periodConcurrencyToken: period.value.concurrencyToken, userIds: addDialog.selected }))
    addDialog.visible = false
    ElMessage.success('人员已加入工资表。')
  } catch (error) { ElMessage.error(api.message(error, '添加人员失败。')) }
  finally { addDialog.loading = false }
}
async function removeEntry(row: PayrollEntry) {
  if (!period.value) return
  try { await ElMessageBox.confirm(`确认从本月工资表移除“${employeeName(row)}”？`, '移除人员', { type: 'warning' }) }
  catch { return }
  loading.value = true
  try {
    setDetail(await api.removePayrollEntry(period.value.id, row.id, { periodConcurrencyToken: period.value.concurrencyToken, entryConcurrencyToken: row.concurrencyToken }))
    ElMessage.success('人员已移除。')
  } catch (error) { ElMessage.error(api.message(error, '移除人员失败。')) }
  finally { loading.value = false }
}
function toInput(row: PayrollEntry): PayrollEntryInput {
  return { id: row.id, entryConcurrencyToken: row.concurrencyToken, baseSalary: row.baseSalary, performanceSalary: row.performanceSalary, bonus: row.bonus, allowance: row.allowance, otherIncrease: row.otherIncrease, socialSecurityDeduction: row.socialSecurityDeduction, housingFundDeduction: row.housingFundDeduction, individualIncomeTax: row.individualIncomeTax, otherDeduction: row.otherDeduction, note: row.note }
}
async function saveRows() {
  if (!period.value || !dirtyRows.value.length) return ElMessage.info('没有需要保存的修改。')
  loading.value = true
  try {
    setDetail(await api.savePayrollEntries(period.value.id, { periodConcurrencyToken: period.value.concurrencyToken, entries: dirtyRows.value.map(toInput) }))
    editDialog.visible = false
    ElMessage.success('工资草稿已保存。')
  } catch (error) { ElMessage.error(api.message(error, '保存工资草稿失败；本地输入已保留。')) }
  finally { loading.value = false }
}
async function lockPeriod() {
  if (!period.value) return
  if (dirtyRows.value.length) return ElMessage.warning('请先保存全部修改，再锁定工资表。')
  try { await ElMessageBox.confirm(`锁定 ${monthLabel(period.value.payrollMonth)}工资表？锁定后金额不可修改。`, '锁定工资表', { type: 'warning', confirmButtonText: '确认锁定' }) }
  catch { return }
  loading.value = true
  try { setDetail(await api.lockPayrollPeriod(period.value.id, period.value.concurrencyToken)); ElMessage.success('工资表已锁定，进入待发放。') }
  catch (error) {
    const problem = error as ApiProblem
    const issues = Object.values(problem.errors ?? {}).flat().join('\n')
    if (issues) await ElMessageBox.alert(issues, problem.message ?? '工资表未锁定', { type: 'error' })
    else ElMessage.error(api.message(error, '锁定工资表失败。'))
  } finally { loading.value = false }
}
async function returnToDraft() {
  if (!period.value) return
  try { await ElMessageBox.confirm('退回草稿后可以继续修改工资，确定继续吗？', '退回草稿', { type: 'warning' }) }
  catch { return }
  loading.value = true
  try { setDetail(await api.returnPayrollPeriodToDraft(period.value.id, period.value.concurrencyToken)); ElMessage.success('工资表已退回草稿。') }
  catch (error) { ElMessage.error(api.message(error, '退回草稿失败。')) }
  finally { loading.value = false }
}
async function cancelPeriod() {
  if (!period.value) return
  try { await ElMessageBox.confirm('取消后本工资表只读保留，可重新创建同月工资表。确定取消吗？', '取消工资表', { type: 'warning', confirmButtonText: '确认取消' }) }
  catch { return }
  loading.value = true
  try { setDetail(await api.cancelPayrollPeriod(period.value.id, period.value.concurrencyToken)); await loadPeriods(period.value.id); ElMessage.success('工资表已取消。') }
  catch (error) { ElMessage.error(api.message(error, '取消工资表失败。')) }
  finally { loading.value = false }
}
function openPayout(row: PayrollEntry) { payoutDialog.row = row; payoutDialog.note = ''; payoutDialog.visible = true }
async function confirmPayout() {
  if (!period.value || !payoutDialog.row) return
  loading.value = true
  try {
    setDetail(await api.confirmPayrollPayout(period.value.id, payoutDialog.row.id, { periodConcurrencyToken: period.value.concurrencyToken, entryConcurrencyToken: payoutDialog.row.concurrencyToken, note: payoutDialog.note.trim() || undefined }))
    payoutDialog.visible = false
    ElMessage.success('已登记工资发放。')
  } catch (error) { ElMessage.error(api.message(error, '确认发放失败。')) }
  finally { loading.value = false }
}
async function exportPeriod() {
  if (!period.value) return
  try {
    const result = await api.exportPayrollPeriod(period.value.id)
    const url = URL.createObjectURL(result.blob)
    const link = document.createElement('a'); link.href = url; link.download = result.fileName; link.click(); URL.revokeObjectURL(url)
    ElMessage.success('工资表已导出。')
  } catch (error) { ElMessage.error(api.message(error, '导出工资表失败。')) }
}
function openEdit(row: PayrollEntry) { editDialog.row = row; editDialog.visible = true }

onMounted(async () => {
  loading.value = true
  try { await loadPeriods() }
  catch (error) { ElMessage.error(api.message(error, '加载工资月份失败。')) }
  finally { loading.value = false }
})
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">MONTHLY PAYROLL LEDGER</p><h1>工资管理</h1><p>按月填写工资，锁定后逐人登记发放。</p></div>
      <div class="page-actions"><el-button :icon="Download" :disabled="!period" @click="exportPeriod">导出 Excel</el-button><el-button circle :icon="Refresh" :loading="loading" @click="loadPeriods(selectedPeriodId)" /><el-button type="primary" :icon="Plus" @click="createDialog.visible = true">新建月份</el-button></div>
    </header>

    <div class="payroll-period-bar">
      <el-select v-model="selectedPeriodId" placeholder="选择工资月份" @change="loadDetail">
        <el-option v-for="item in periods" :key="item.id" :label="`${monthLabel(item.payrollMonth)} · ${statusLabels[item.status]}`" :value="item.id" />
      </el-select>
      <template v-if="period"><el-tag :type="statusType(period.status)" effect="plain">{{ statusLabels[period.status] }}</el-tag><span>更新于 {{ dateTime(period.updatedAt) }}</span></template>
    </div>

    <template v-if="period">
      <div class="payroll-summary">
        <div><span>工资人数</span><strong>{{ period.employeeCount }}</strong></div>
        <div><span>应发合计</span><strong>{{ money(period.grossTotal) }}</strong></div>
        <div><span>扣款合计</span><strong>{{ money(period.deductionTotal) }}</strong></div>
        <div class="payroll-summary__net"><span>实发合计</span><strong>{{ money(period.netTotal) }}</strong></div>
        <div><span>发放进度</span><strong>{{ period.paidCount }}/{{ period.employeeCount }}</strong><small>已发 {{ money(period.paidNetTotal) }} · 待发 {{ money(period.pendingNetTotal) }}</small></div>
      </div>

      <div class="payroll-actions">
        <div class="filter-cluster"><el-input v-model="keyword" clearable placeholder="搜索人员" /><el-select v-model="payoutFilter" clearable placeholder="全部发放状态"><el-option v-for="option in payoutOptions" :key="option.value" :label="option.label" :value="option.value" /></el-select></div>
        <div><el-button v-if="isDraft" @click="openAdd">添加人员</el-button><el-button v-if="isDraft" :disabled="!dirtyRows.length" type="primary" @click="saveRows">保存修改（{{ dirtyRows.length }}）</el-button><el-button v-if="isDraft" type="success" @click="lockPeriod">锁定工资表</el-button><el-button v-if="isDraft" type="danger" plain @click="cancelPeriod">取消工资表</el-button><el-button v-if="period.status === 'ReadyForPayout' && period.paidCount === 0" @click="returnToDraft">退回草稿</el-button></div>
      </div>

      <div class="table-shell payroll-table desktop-table" v-loading="loading">
        <el-table :data="filteredRows" row-key="id" empty-text="当前没有工资明细。">
          <el-table-column label="员工" fixed width="150"><template #default="scope"><div class="primary-cell"><strong>{{ employeeName(scope.row) }}</strong><span>{{ scope.row.employeeDisplayNameSnapshot }}</span></div></template></el-table-column>
          <el-table-column label="收入项">
            <el-table-column v-for="field in [{key:'baseSalary',label:'基本工资'},{key:'performanceSalary',label:'绩效工资'},{key:'bonus',label:'奖金'},{key:'allowance',label:'津贴'},{key:'otherIncrease',label:'其他增加'}]" :key="field.key" :label="field.label" width="138">
              <template #default="scope"><el-input-number v-if="isDraft" v-model="scope.row[field.key]" :min="0" :precision="2" :controls="false" /><span v-else>{{ money(scope.row[field.key]) }}</span></template>
            </el-table-column>
          </el-table-column>
          <el-table-column label="扣款项">
            <el-table-column v-for="field in [{key:'socialSecurityDeduction',label:'社保'},{key:'housingFundDeduction',label:'公积金'},{key:'individualIncomeTax',label:'个税'},{key:'otherDeduction',label:'其他扣款'}]" :key="field.key" :label="field.label" width="138">
              <template #default="scope"><el-input-number v-if="isDraft" v-model="scope.row[field.key]" :min="0" :precision="2" :controls="false" /><span v-else>{{ money(scope.row[field.key]) }}</span></template>
            </el-table-column>
          </el-table-column>
          <el-table-column label="应发" width="120"><template #default="scope">{{ money(gross(scope.row)) }}</template></el-table-column>
          <el-table-column label="扣款" width="120"><template #default="scope">{{ money(deductions(scope.row)) }}</template></el-table-column>
          <el-table-column label="实发" width="125"><template #default="scope"><strong>{{ money(net(scope.row)) }}</strong></template></el-table-column>
          <el-table-column label="状态" width="100"><template #default="scope"><el-tag :type="payoutType(scope.row.payoutStatus)" effect="plain">{{ payoutLabel(scope.row.payoutStatus) }}</el-tag></template></el-table-column>
          <el-table-column label="操作" fixed="right" width="155"><template #default="scope"><el-button v-if="scope.row.payoutStatus === 'Pending'" size="small" type="primary" @click="openPayout(scope.row)">确认已发放</el-button><el-button v-if="isDraft" size="small" @click="openEdit(scope.row)">备注</el-button><el-button v-if="isDraft" size="small" type="danger" text @click="removeEntry(scope.row)">移除</el-button></template></el-table-column>
        </el-table>
      </div>

      <div class="mobile-list" v-loading="loading">
        <article v-for="row in filteredRows" :key="row.id" class="mobile-record payroll-mobile-card">
          <div class="mobile-record__head"><div><strong>{{ employeeName(row) }}</strong><span>{{ row.employeeDisplayNameSnapshot }}</span></div><el-tag :type="payoutType(row.payoutStatus)" effect="plain">{{ payoutLabel(row.payoutStatus) }}</el-tag></div>
          <dl><div><dt>应发</dt><dd>{{ money(gross(row)) }}</dd></div><div><dt>扣款</dt><dd>{{ money(deductions(row)) }}</dd></div><div><dt>实发</dt><dd><strong>{{ money(net(row)) }}</strong></dd></div></dl>
          <div class="mobile-record__actions"><el-button v-if="isDraft" @click="openEdit(row)">编辑工资</el-button><el-button v-if="row.payoutStatus === 'Pending'" type="primary" @click="openPayout(row)">确认已发放</el-button><el-button v-if="isDraft" type="danger" plain @click="removeEntry(row)">移除</el-button></div>
        </article>
        <el-empty v-if="!loading && filteredRows.length === 0" description="当前没有工资明细" />
      </div>
    </template>
    <el-empty v-else description="尚未创建工资表"><el-button type="primary" @click="createDialog.visible = true">创建本月工资表</el-button></el-empty>

    <el-dialog v-model="createDialog.visible" title="新建工资表" width="min(420px, calc(100vw - 32px))"><el-form label-position="top"><el-form-item label="工资月份"><el-date-picker v-model="createDialog.month" type="month" value-format="YYYY-MM" placeholder="选择月份" style="width:100%" /></el-form-item></el-form><template #footer><el-button @click="createDialog.visible = false">取消</el-button><el-button type="primary" :loading="loading" @click="createPeriod">创建</el-button></template></el-dialog>

    <el-dialog v-model="addDialog.visible" title="添加工资人员" width="min(620px, calc(100vw - 32px))"><div class="payroll-candidate-toolbar"><el-input v-model="addDialog.keyword" clearable placeholder="搜索正式用户" @keyup.enter="loadCandidates" /><el-button @click="loadCandidates">搜索</el-button></div><el-checkbox-group v-model="addDialog.selected" class="payroll-candidates" v-loading="addDialog.loading"><el-checkbox v-for="candidate in addDialog.candidates" :key="candidate.id" :value="candidate.id"><span>{{ candidate.personalName || candidate.displayName }}</span><small>{{ candidate.isActive ? '启用' : '停用' }} · 姓名{{ candidate.personalNameReady ? '已填' : '缺失' }} · 银行卡{{ candidate.bankCardReady ? '已填' : '缺失' }}</small></el-checkbox></el-checkbox-group><el-empty v-if="!addDialog.loading && !addDialog.candidates.length" description="没有可添加的正式用户" /><template #footer><el-button @click="addDialog.visible = false">取消</el-button><el-button type="primary" :loading="addDialog.loading" @click="addEntries">添加</el-button></template></el-dialog>

    <el-dialog v-model="editDialog.visible" title="编辑工资" width="min(620px, calc(100vw - 16px))" class="payroll-editor-dialog">
      <template v-if="editDialog.row"><div class="dialog-subject"><strong>{{ employeeName(editDialog.row) }}</strong><span>{{ monthLabel(period!.payrollMonth) }}</span></div><div class="payroll-editor-grid"><el-form-item v-for="field in [{key:'baseSalary',label:'基本工资'},{key:'performanceSalary',label:'绩效工资'},{key:'bonus',label:'奖金'},{key:'allowance',label:'津贴'},{key:'otherIncrease',label:'其他增加'},{key:'socialSecurityDeduction',label:'社保个人扣款'},{key:'housingFundDeduction',label:'公积金个人扣款'},{key:'individualIncomeTax',label:'个人所得税'},{key:'otherDeduction',label:'其他扣款'}]" :key="field.key" :label="field.label"><el-input-number v-model="editDialog.row[field.key]" :min="0" :precision="2" :controls="false" /></el-form-item></div><div class="payroll-editor-result"><span>应发 {{ money(gross(editDialog.row)) }}</span><span>扣款 {{ money(deductions(editDialog.row)) }}</span><strong>实发 {{ money(net(editDialog.row)) }}</strong></div><el-form-item label="备注（其他增加或其他扣款不为 0 时必填）"><el-input v-model="editDialog.row.note" type="textarea" :rows="3" maxlength="1000" show-word-limit /></el-form-item></template>
      <template #footer><el-button @click="editDialog.visible = false">暂不保存</el-button><el-button type="primary" :loading="loading" @click="saveRows">保存修改</el-button></template>
    </el-dialog>

    <el-dialog v-model="payoutDialog.visible" title="确认工资已发放" width="min(480px, calc(100vw - 32px))"><template v-if="payoutDialog.row"><div class="payroll-payout-check"><span>员工</span><strong>{{ employeeName(payoutDialog.row) }}</strong><span>实发金额</span><strong>{{ money(payoutDialog.row.netPay) }}</strong><span>收款账户</span><strong>•••• {{ payoutDialog.row.bankCardLastFourSnapshot }}</strong></div><el-alert title="本操作仅登记已发放，不执行银行转账，确认后不可撤销。" type="warning" :closable="false" show-icon /><el-input v-model="payoutDialog.note" type="textarea" :rows="3" maxlength="1000" placeholder="发放备注（可选）" /></template><template #footer><el-button @click="payoutDialog.visible = false">取消</el-button><el-button type="primary" :loading="loading" @click="confirmPayout">确认已发放</el-button></template></el-dialog>
  </section>
</template>
