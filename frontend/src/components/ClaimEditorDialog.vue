<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { ElMessage, type UploadFile } from 'element-plus'
import { Delete, Download, Plus, Upload, View } from '@element-plus/icons-vue'
import {
  api,
  type Attachment,
  type ClaimDetail,
  type ClaimDraftPayload,
  type ClaimType,
  type ExpenseCategory,
  type Project,
} from '../api'
import AttachmentPreviewDialog from './AttachmentPreviewDialog.vue'

type EditorExpenseItem = {
  clientKey: string
  category: '' | ExpenseCategory
  amount?: number
  expenseDate: string
  merchant: string
  note: string
  attachments: Attachment[]
  uploading: boolean
}

const props = defineProps<{ modelValue: boolean; claimId?: string | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean]; saved: [] }>()

const dialogOpen = computed({ get: () => props.modelValue, set: value => emit('update:modelValue', value) })
const loading = ref(false)
const projects = ref<Project[]>([])
const currentClaim = ref<ClaimDetail | null>(null)
const previewOpen = ref(false)
const previewTarget = ref<Attachment | null>(null)
const form = reactive({
  type: 'Travel' as ClaimType,
  projectId: '',
  description: '',
  departureLocation: '',
  destination: '',
  departureDate: '',
  returnDate: '',
  expenseItems: [] as EditorExpenseItem[],
})

const typeLabels: Record<ClaimType, string> = { Travel: '差旅行程', General: '普通单据' }
const categoryLabels: Record<ExpenseCategory, string> = {
  DepartureTransport: '去程交通',
  ReturnTransport: '回程交通',
  Lodging: '住宿',
  OfficeSupplies: '办公用品',
  Meal: '餐费',
  Other: '其他',
  Unspecified: '未选择',
}
const categoryOptions: ExpenseCategory[] = ['DepartureTransport', 'ReturnTransport', 'Lodging', 'OfficeSupplies', 'Meal', 'Other']
const totalAmount = computed(() => form.expenseItems.reduce((sum, item) => sum + Number(item.amount ?? 0), 0))
const title = computed(() => currentClaim.value ? `编辑报销 · ${currentClaim.value.claimNumber}` : '新增报销')
const mealAllowanceDays = computed(() => {
  if (form.type !== 'Travel' || !form.departureDate || !form.returnDate || form.returnDate < form.departureDate) return null
  const [departureYear, departureMonth, departureDay] = form.departureDate.split('-').map(Number)
  const [returnYear, returnMonth, returnDay] = form.returnDate.split('-').map(Number)
  const departureUtc = Date.UTC(departureYear!, departureMonth! - 1, departureDay)
  const returnUtc = Date.UTC(returnYear!, returnMonth! - 1, returnDay)
  return Math.floor((returnUtc - departureUtc) / 86_400_000) + 1
})

function newClientKey() {
  const secureUuid = globalThis.crypto?.randomUUID?.()
  if (secureUuid) return secureUuid

  const bytes = new Uint8Array(16)
  for (let index = 0; index < bytes.length; index += 1) bytes[index] = Math.floor(Math.random() * 256)
  bytes[6] = (bytes[6]! & 0x0f) | 0x40
  bytes[8] = (bytes[8]! & 0x3f) | 0x80
  const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

function newExpenseItem(category: '' | ExpenseCategory = ''): EditorExpenseItem {
  return { clientKey: newClientKey(), category, amount: undefined, expenseDate: '', merchant: '', note: '', attachments: [], uploading: false }
}

function defaultExpenseItems(type: ClaimType) {
  return type === 'Travel'
    ? [newExpenseItem('DepartureTransport'), newExpenseItem('ReturnTransport')]
    : [newExpenseItem()]
}

function resetForm() {
  currentClaim.value = null
  form.type = 'Travel'
  form.projectId = ''
  form.description = ''
  form.departureLocation = ''
  form.destination = ''
  form.departureDate = ''
  form.returnDate = ''
  form.expenseItems = defaultExpenseItems('Travel')
}

async function initialize() {
  resetForm()
  loading.value = true
  try {
    projects.value = await api.listAvailableProjects()
    if (!props.claimId) return
    const claim = await api.getClaim(props.claimId)
    currentClaim.value = claim
    form.type = claim.type
    form.projectId = claim.currentVersion.projectId
    form.description = claim.currentVersion.description
    form.departureLocation = claim.currentVersion.travelItinerary?.departureLocation ?? ''
    form.destination = claim.currentVersion.travelItinerary?.destination ?? ''
    form.departureDate = claim.currentVersion.travelItinerary?.departureDate ?? ''
    form.returnDate = claim.currentVersion.travelItinerary?.returnDate ?? ''
    form.expenseItems = claim.currentVersion.expenseItems.map(item => ({
      clientKey: item.clientKey,
      category: item.category === 'Unspecified' ? '' : item.category,
      amount: item.amount ?? undefined,
      expenseDate: item.expenseDate ?? '',
      merchant: item.merchant ?? '',
      note: item.note ?? '',
      attachments: [...item.attachments],
      uploading: false,
    }))
    if (!projects.value.some(project => project.id === claim.currentVersion.projectId)) {
      projects.value.push({
        id: claim.currentVersion.projectId,
        code: claim.currentVersion.project.code,
        name: claim.currentVersion.project.name,
        isActive: false,
        concurrencyToken: '',
      })
    }
  } catch (error) {
    ElMessage.error(api.message(error, '加载报销内容失败。'))
    dialogOpen.value = false
  } finally {
    loading.value = false
  }
}

function handleTypeChange(type: ClaimType) {
  if (!currentClaim.value) form.expenseItems = defaultExpenseItems(type)
}
function addExpenseItem() { form.expenseItems.push(newExpenseItem()) }
function removeExpenseItem(index: number) { form.expenseItems.splice(index, 1) }
function removeAttachment(item: EditorExpenseItem, attachmentId: string) {
  item.attachments = item.attachments.filter(attachment => attachment.id !== attachmentId)
}

function previewAttachment(attachment: Attachment) {
  previewTarget.value = attachment
  previewOpen.value = true
}

async function uploadAttachment(item: EditorExpenseItem, uploadFile: UploadFile) {
  const file = uploadFile.raw
  if (!file) return
  if (file.size > 10 * 1024 * 1024) { ElMessage.error('单个凭证不能超过 10MB。'); return }
  item.uploading = true
  try {
    item.attachments.push(await api.uploadStagedAttachment(file))
    ElMessage.success('凭证已上传。')
  } catch (error) {
    ElMessage.error(api.message(error, '凭证上传失败。'))
  } finally {
    item.uploading = false
  }
}

function uploadHandler(item: EditorExpenseItem) {
  return (file: UploadFile) => uploadAttachment(item, file)
}

async function downloadAttachment(attachment: Attachment) {
  try {
    const result = await api.downloadAttachment(attachment.id)
    const url = URL.createObjectURL(result.blob)
    const link = document.createElement('a')
    link.href = url
    link.download = result.fileName || attachment.originalFileName
    link.click()
    URL.revokeObjectURL(url)
  } catch (error) {
    ElMessage.error(api.message(error, '下载凭证失败。'))
  }
}

function payload(): ClaimDraftPayload | null {
  if (!form.projectId) { ElMessage.error('请选择项目。'); return null }
  return {
    projectId: form.projectId,
    description: form.description.trim(),
    travelItinerary: form.type === 'Travel' ? {
      departureLocation: form.departureLocation.trim() || null,
      destination: form.destination.trim() || null,
      departureDate: form.departureDate || null,
      returnDate: form.returnDate || null,
    } : null,
    expenseItems: form.expenseItems.map(item => ({
      clientKey: item.clientKey,
      category: item.category || 'Unspecified',
      amount: item.amount ?? null,
      expenseDate: item.expenseDate || null,
      merchant: item.merchant.trim() || null,
      note: item.note.trim() || null,
      attachmentIds: item.attachments.map(attachment => attachment.id),
    })),
  }
}

function validateForSubmit(): string | null {
  if (!form.projectId) return '请选择所属项目。'
  if (!form.description.trim()) return '请填写报销说明。'

  if (form.type === 'Travel') {
    if (!form.departureLocation.trim()) return '请填写出发地。'
    if (!form.destination.trim()) return '请填写目的地。'
    if (!form.departureDate) return '请选择出发日期。'
    if (!form.returnDate) return '请选择返回日期。'
    if (form.returnDate < form.departureDate) return '返回日期不能早于出发日期。'
  }

  if (form.expenseItems.length === 0) return '请至少添加一项费用明细。'
  for (const [index, item] of form.expenseItems.entries()) {
    const itemLabel = `费用 ${index + 1}`
    if (!item.category) return `请选择${itemLabel}的类别。`
    if (!item.amount || item.amount <= 0) return `请填写${itemLabel}的金额。`
    if (!item.expenseDate) return `请选择${itemLabel}的费用日期。`
    if (!item.merchant.trim()) return `请填写${itemLabel}的商户或承运方。`
    if (item.attachments.length === 0) return `请上传${itemLabel}的有效凭证。`
  }

  if (form.type === 'Travel') {
    if (!form.expenseItems.some(item => item.category === 'DepartureTransport')) return '请至少添加一项去程交通费用。'
    if (!form.expenseItems.some(item => item.category === 'ReturnTransport')) return '请至少添加一项回程交通费用。'
  }
  return null
}

async function persistDraft() {
  const draft = payload()
  if (!draft) return null
  if (!currentClaim.value) {
    currentClaim.value = await api.createClaim(form.type, draft)
  } else {
    currentClaim.value = await api.createClaimVersion(currentClaim.value.id, {
      ...draft,
      expectedCurrentVersionId: currentClaim.value.currentVersionId,
      concurrencyToken: currentClaim.value.concurrencyToken,
    })
  }
  return currentClaim.value
}

async function saveDraft() {
  loading.value = true
  try {
    const saved = await persistDraft()
    if (!saved) return
    ElMessage.success(`草稿已保存为 v${saved.currentVersion.versionNumber}。`)
    emit('saved')
    dialogOpen.value = false
  } catch (error) {
    ElMessage.error(api.message(error, '保存草稿失败。'))
    if ((error as { status?: number }).status === 409) await initialize()
  } finally {
    loading.value = false
  }
}

async function submit() {
  const validationError = validateForSubmit()
  if (validationError) { ElMessage.error(validationError); return }
  loading.value = true
  try {
    const saved = await persistDraft()
    if (!saved) return
    await api.submitClaim(saved.id, { expectedCurrentVersionId: saved.currentVersionId, concurrencyToken: saved.concurrencyToken })
    ElMessage.success('报销已提交审核。')
    emit('saved')
    dialogOpen.value = false
  } catch (error) {
    ElMessage.error(api.message(error, '提交报销失败。'))
    if ((error as { status?: number }).status === 409) await initialize()
  } finally {
    loading.value = false
  }
}

function handleDialogOpen() { initialize() }
</script>

<template>
  <el-dialog v-model="dialogOpen" :title="title" width="min(980px, calc(100vw - 32px))" :close-on-click-modal="false" class="claim-editor-dialog" @open="handleDialogOpen">
    <div v-loading="loading" class="claim-editor">
      <div v-if="currentClaim" class="version-notice">
        <strong>当前 v{{ currentClaim.currentVersion.versionNumber }} · {{ typeLabels[currentClaim.type] }}</strong>
        <span>保存修改会生成新版本，当前版本转为只读历史；已提交或驳回的报销会回到草稿。</span>
      </div>

      <p class="required-hint"><span aria-hidden="true">*</span> 提交审核时必填；保存草稿仅需选择所属项目。</p>

      <el-form label-position="top">
        <div class="claim-form-grid">
          <el-form-item label="报销类型" required>
            <el-radio-group v-model="form.type" :disabled="!!currentClaim" @change="handleTypeChange">
              <el-radio-button value="Travel">差旅行程</el-radio-button>
              <el-radio-button value="General">普通单据</el-radio-button>
            </el-radio-group>
          </el-form-item>
          <el-form-item label="所属项目" required>
            <el-select v-model="form.projectId" filterable placeholder="选择启用项目">
              <el-option v-for="project in projects" :key="project.id" :value="project.id" :label="`${project.code} · ${project.name}${project.isActive ? '' : '（已停用）'}`" />
            </el-select>
          </el-form-item>
        </div>
        <el-form-item label="报销说明" required>
          <el-input v-model="form.description" type="textarea" :rows="3" maxlength="1000" show-word-limit placeholder="说明本次费用用途" />
        </el-form-item>

        <section v-if="form.type === 'Travel'" class="editor-section">
          <div class="section-heading"><div><h3>行程信息</h3><p>提交审核前需完整填写往返地点和日期。</p></div></div>
          <div class="itinerary-grid">
            <el-form-item label="出发地" required><el-input v-model="form.departureLocation" maxlength="100" /></el-form-item>
            <el-form-item label="目的地" required><el-input v-model="form.destination" maxlength="100" /></el-form-item>
            <el-form-item label="出发日期" required><el-date-picker v-model="form.departureDate" type="date" value-format="YYYY-MM-DD" placeholder="选择日期" /></el-form-item>
            <el-form-item label="返回日期" required><el-date-picker v-model="form.returnDate" type="date" value-format="YYYY-MM-DD" placeholder="选择日期" /></el-form-item>
          </div>
          <div class="meal-preview" :class="{ 'meal-preview--pending': mealAllowanceDays === null }">
            <div><span>同步申请餐补</span><strong>{{ mealAllowanceDays === null ? '待确定' : `${mealAllowanceDays} 天` }}</strong></div>
            <p>{{ mealAllowanceDays === null ? '选择有效的出发和返回日期后自动计算。' : '按行程自然日含首尾计算，当天往返计 1 天；餐补金额由管理员在差旅审批通过后另行审核。' }}</p>
          </div>
        </section>

        <section class="editor-section">
          <div class="section-heading">
            <div><h3>费用明细 <span class="required-mark" aria-hidden="true">*</span></h3><p>至少添加一项；每项提交前都需填写完整并上传有效凭证。</p></div>
          </div>
          <div class="expense-list">
            <article v-for="(item, index) in form.expenseItems" :key="item.clientKey" class="expense-item">
              <div class="expense-item__head"><strong>费用 {{ index + 1 }}</strong><el-tooltip content="删除此费用"><el-button text circle type="danger" :icon="Delete" aria-label="删除费用" @click="removeExpenseItem(index)" /></el-tooltip></div>
              <div class="expense-grid">
                <el-form-item label="类别" required><el-select v-model="item.category" placeholder="请选择类别"><el-option v-for="category in categoryOptions" :key="category" :value="category" :label="categoryLabels[category]" /></el-select></el-form-item>
                <el-form-item label="金额" required><el-input-number v-model="item.amount" :min="0.01" :precision="2" :controls="false" placeholder="0.00" /></el-form-item>
                <el-form-item label="费用日期" required><el-date-picker v-model="item.expenseDate" type="date" value-format="YYYY-MM-DD" placeholder="选择日期" /></el-form-item>
                <el-form-item label="商户 / 承运方" required><el-input v-model="item.merchant" maxlength="200" /></el-form-item>
              </div>
              <el-form-item label="备注"><el-input v-model="item.note" maxlength="500" placeholder="可选" /></el-form-item>
              <el-form-item label="凭证" required class="attachment-form-item">
                <div class="attachment-field">
                  <div class="attachment-row">
                    <el-upload accept=".jpg,.jpeg,.png,.pdf" :auto-upload="false" :show-file-list="false" :on-change="uploadHandler(item)">
                      <el-button :icon="Upload" :loading="item.uploading">上传凭证</el-button>
                    </el-upload>
                    <span class="attachment-hint">JPG、PNG 或 PDF，单个不超过 10MB</span>
                  </div>
                  <div v-if="item.attachments.length" class="attachment-list">
                    <div v-for="attachment in item.attachments" :key="attachment.id" class="attachment-file">
                      <span>{{ attachment.originalFileName }}</span>
                      <div><el-tooltip content="在线预览"><el-button text circle :icon="View" aria-label="预览凭证" @click="previewAttachment(attachment)" /></el-tooltip><el-tooltip content="下载凭证"><el-button text circle :icon="Download" aria-label="下载凭证" @click="downloadAttachment(attachment)" /></el-tooltip><el-tooltip content="从本版本移除"><el-button text circle type="danger" :icon="Delete" aria-label="移除凭证" @click="removeAttachment(item, attachment.id)" /></el-tooltip></div>
                    </div>
                  </div>
                </div>
              </el-form-item>
            </article>
            <el-empty v-if="form.expenseItems.length === 0" description="还没有费用明细" :image-size="72" />
            <div class="expense-add-row"><el-button plain :icon="Plus" @click="addExpenseItem">添加费用</el-button></div>
          </div>
        </section>
      </el-form>
    </div>
    <template #footer>
      <div class="claim-editor-footer">
        <span>当前合计 <strong>{{ totalAmount.toLocaleString('zh-CN', { style: 'currency', currency: 'CNY' }) }}</strong></span>
        <div><el-button @click="dialogOpen = false">取消</el-button><el-button :loading="loading" @click="saveDraft">保存草稿</el-button><el-button type="primary" :loading="loading" @click="submit">提交审核</el-button></div>
      </div>
    </template>
  </el-dialog>
  <AttachmentPreviewDialog v-model="previewOpen" :attachment="previewTarget" />
</template>
