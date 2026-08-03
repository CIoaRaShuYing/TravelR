<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Download, View } from '@element-plus/icons-vue'
import { api, type Attachment, type ClaimDetail, type ClaimVersion, type ClaimVersionSummary } from '../api'
import AttachmentPreviewDialog from './AttachmentPreviewDialog.vue'

const props = withDefaults(defineProps<{ modelValue: boolean; claimId?: string | null; includeSupersededVersions?: boolean }>(), {
  includeSupersededVersions: true,
})
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()
const drawerOpen = computed({ get: () => props.modelValue, set: value => emit('update:modelValue', value) })
const loading = ref(false)
const detail = ref<ClaimDetail | null>(null)
const versions = ref<ClaimVersionSummary[]>([])
const selectedVersion = ref<ClaimVersion | null>(null)
const previewOpen = ref(false)
const previewTarget = ref<Attachment | null>(null)
const showVersionHistory = computed(() => props.includeSupersededVersions && versions.value.length > 1)

const statusLabels: Record<string, string> = { Draft: '草稿', Submitted: '待审批', Approved: '已批准', Rejected: '已驳回', Cancelled: '已作废' }
const payoutLabels: Record<string, string> = { NotApplicable: '无需发放', Pending: '待发放', Paid: '已发放' }
const categoryLabels: Record<string, string> = { DepartureTransport: '去程交通', ReturnTransport: '回程交通', Lodging: '住宿', OfficeSupplies: '办公用品', Meal: '餐费', Other: '其他' }

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function dateTime(value?: string | null) { return value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '-' }
function previewAttachment(attachment: Attachment) { previewTarget.value = attachment; previewOpen.value = true }

async function initialize() {
  if (!props.claimId) return
  loading.value = true
  try {
    const [claim, history] = await Promise.all([api.getClaim(props.claimId), api.getClaimVersions(props.claimId)])
    detail.value = claim
    versions.value = history
    selectedVersion.value = claim.currentVersion
  } catch (error) {
    ElMessage.error(api.message(error, '加载报销详情失败。'))
    drawerOpen.value = false
  } finally {
    loading.value = false
  }
}

async function selectVersion(version: ClaimVersionSummary) {
  if (!detail.value || selectedVersion.value?.id === version.id) return
  loading.value = true
  try {
    selectedVersion.value = version.isCurrent
      ? detail.value.currentVersion
      : await api.getClaimVersion(detail.value.id, version.id)
  } catch (error) {
    ElMessage.error(api.message(error, '加载历史版本失败。'))
  } finally {
    loading.value = false
  }
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

watch(() => props.modelValue, open => { if (open) initialize() })
</script>

<template>
  <el-drawer v-model="drawerOpen" size="min(860px, 94vw)" :with-header="false" class="claim-detail-drawer">
    <div v-loading="loading" class="claim-detail">
      <header v-if="detail" class="detail-header">
        <div><p class="eyebrow">CLAIM LEDGER</p><h2>{{ detail.claimNumber }}</h2><p>{{ detail.applicant.displayName }} · {{ detail.currentVersion.project.code }} · {{ detail.currentVersion.project.name }}</p></div>
        <div class="detail-statuses"><el-tag effect="plain">{{ statusLabels[detail.status] }}</el-tag><el-tag :type="detail.payoutStatus === 'Paid' ? 'success' : detail.payoutStatus === 'Pending' ? 'warning' : 'info'" effect="plain">{{ payoutLabels[detail.payoutStatus] }}</el-tag></div>
      </header>

      <template v-if="detail && selectedVersion">
        <div class="version-layout" :class="{ 'version-layout--single': !showVersionHistory }">
          <aside v-if="showVersionHistory" class="version-rail">
            <p>版本历史</p>
            <button v-for="version in versions" :key="version.id" type="button" :class="{ active: selectedVersion.id === version.id }" @click="selectVersion(version)">
              <span>v{{ version.versionNumber }} <em v-if="version.isCurrent">当前</em><em v-else>已作废</em></span>
              <strong>{{ money(version.totalAmount) }}</strong>
              <small>{{ dateTime(version.createdAt) }}</small>
            </button>
          </aside>

          <section class="version-content">
            <div class="version-heading"><div><span>v{{ selectedVersion.versionNumber }}</span><h3>{{ selectedVersion.project.name }}</h3><p>{{ selectedVersion.project.code }}</p></div><strong>{{ money(selectedVersion.totalAmount) }}</strong></div>
            <dl class="detail-facts">
              <div><dt>报销说明</dt><dd>{{ selectedVersion.description || '-' }}</dd></div>
              <div><dt>版本时间</dt><dd>{{ dateTime(selectedVersion.createdAt) }}</dd></div>
              <div v-if="selectedVersion.travelItinerary"><dt>差旅行程</dt><dd>{{ selectedVersion.travelItinerary.departureLocation || '-' }} → {{ selectedVersion.travelItinerary.destination || '-' }}<br>{{ selectedVersion.travelItinerary.departureDate || '-' }} 至 {{ selectedVersion.travelItinerary.returnDate || '-' }}</dd></div>
            </dl>

            <div class="detail-section-heading"><h3>费用明细</h3><span>{{ selectedVersion.expenseItems.length }} 项</span></div>
            <div class="detail-expenses">
              <article v-for="item in selectedVersion.expenseItems" :key="item.id">
                <div class="detail-expense__head"><strong>{{ categoryLabels[item.category] }}</strong><span>{{ money(item.amount ?? 0) }}</span></div>
                <dl><div><dt>日期</dt><dd>{{ item.expenseDate || '-' }}</dd></div><div><dt>商户</dt><dd>{{ item.merchant || '-' }}</dd></div><div v-if="item.note"><dt>备注</dt><dd>{{ item.note }}</dd></div></dl>
                <div v-if="item.attachments.length" class="detail-attachments">
                  <div v-for="attachment in item.attachments" :key="attachment.id" class="detail-attachment-file"><span>{{ attachment.originalFileName }}</span><div><el-tooltip content="在线预览"><el-button text circle :icon="View" aria-label="预览凭证" @click="previewAttachment(attachment)" /></el-tooltip><el-tooltip content="下载凭证"><el-button text circle :icon="Download" aria-label="下载凭证" @click="downloadAttachment(attachment)" /></el-tooltip></div></div>
                </div>
              </article>
              <el-empty v-if="selectedVersion.expenseItems.length === 0" description="该版本没有费用明细" :image-size="64" />
            </div>

            <template v-if="detail.approvalRecords.length">
              <div class="detail-section-heading"><h3>状态记录</h3><span>{{ detail.approvalRecords.length }} 条</span></div>
              <el-timeline class="approval-timeline">
                <el-timeline-item v-for="record in detail.approvalRecords" :key="`${record.claimVersionId}-${record.createdAt}`" :timestamp="dateTime(record.createdAt)">
                  <strong>v{{ record.versionNumber }} · {{ statusLabels[record.fromStatus] }} → {{ statusLabels[record.toStatus] }}</strong><p v-if="record.comment">{{ record.comment }}</p>
                </el-timeline-item>
              </el-timeline>
            </template>

            <div v-if="detail.payoutRecord" class="payout-receipt"><span>发放记录</span><strong>{{ money(detail.payoutRecord.amount) }}</strong><p>{{ dateTime(detail.payoutRecord.confirmedAt) }}{{ detail.payoutRecord.note ? ` · ${detail.payoutRecord.note}` : '' }}</p></div>
          </section>
        </div>
      </template>
    </div>
  </el-drawer>
  <AttachmentPreviewDialog v-model="previewOpen" :attachment="previewTarget" />
</template>
