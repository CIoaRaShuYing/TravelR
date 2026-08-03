<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Download } from '@element-plus/icons-vue'
import { api, type Attachment } from '../api'

const props = defineProps<{ modelValue: boolean; attachment?: Attachment | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const dialogOpen = computed({ get: () => props.modelValue, set: value => emit('update:modelValue', value) })
const loading = ref(false)
const previewUrl = ref('')
const previewType = ref('')
const previewFailed = ref(false)
const isImage = computed(() => previewType.value.startsWith('image/'))
const isPdf = computed(() => previewType.value === 'application/pdf')

function releasePreview() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  previewType.value = ''
  previewFailed.value = false
}

async function loadPreview() {
  const attachment = props.attachment
  if (!attachment) return
  releasePreview()
  loading.value = true
  try {
    const result = await api.downloadAttachment(attachment.id)
    previewType.value = result.blob.type || attachment.contentType
    previewUrl.value = URL.createObjectURL(result.blob)
  } catch (error) {
    previewFailed.value = true
    ElMessage.error(api.message(error, '加载凭证预览失败。'))
  } finally {
    loading.value = false
  }
}

async function download() {
  const attachment = props.attachment
  if (!attachment) return
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

watch([() => props.modelValue, () => props.attachment?.id], ([open]) => {
  if (open) loadPreview()
  else releasePreview()
})
onBeforeUnmount(releasePreview)
</script>

<template>
  <el-dialog v-model="dialogOpen" append-to-body destroy-on-close :title="attachment?.originalFileName || '凭证预览'" width="min(920px, calc(100vw - 32px))" class="attachment-preview-dialog">
    <div v-loading="loading" class="attachment-preview-stage">
      <img v-if="previewUrl && isImage" :src="previewUrl" :alt="attachment?.originalFileName || '凭证图片'">
      <iframe v-else-if="previewUrl && isPdf" :src="previewUrl" :title="attachment?.originalFileName || 'PDF 凭证预览'" />
      <el-result v-else-if="previewFailed" icon="error" title="预览加载失败" sub-title="可以关闭窗口后重试，或直接下载文件。" />
      <el-result v-else-if="previewUrl" icon="warning" title="此文件无法在线预览" sub-title="请下载后使用本地应用打开。" />
    </div>
    <template #footer>
      <el-button @click="dialogOpen = false">关闭</el-button>
      <el-button :icon="Download" :disabled="!attachment" @click="download">下载文件</el-button>
    </template>
  </el-dialog>
</template>
