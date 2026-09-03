<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { ArrowDown, ArrowUp, Delete, Plus, Refresh } from '@element-plus/icons-vue'
import type { MeetingRecordDetail, MeetingRecordPayload, MeetingRecordProject } from '../api'

type ParticipantDraft = { clientKey: string; name: string; organization: string; title: string; phone: string }
type ItemDraft = { clientKey: string; content: string; status: string; dueDate: string; owner: string }

const props = defineProps<{
  record: MeetingRecordDetail | null
  projects: MeetingRecordProject[]
  saving: boolean
  loading: boolean
  stale: boolean
}>()
const visible = defineModel<boolean>({ required: true })
const emit = defineEmits<{ save: [payload: MeetingRecordPayload]; reload: [] }>()
const form = reactive({ projectId: '', meetingDate: '', location: '', participants: [] as ParticipantDraft[], items: [] as ItemDraft[] })
const title = computed(() => props.record ? `编辑 ${props.record.meetingDate} 会议记录` : '新建会议记录')
const availableProjects = computed(() => props.projects.filter(project => project.isActive || project.id === props.record?.projectId))

function key() { return typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}` }
function participant(): ParticipantDraft { return { clientKey: key(), name: '', organization: '', title: '', phone: '' } }
function item(): ItemDraft { return { clientKey: key(), content: '', status: '', dueDate: '', owner: '' } }

function reset() {
  const record = props.record
  Object.assign(form, {
    projectId: record?.projectId ?? '',
    meetingDate: record?.meetingDate ?? '',
    location: record?.location ?? '',
    participants: record?.participants.map(value => ({ clientKey: key(), name: value.name, organization: value.organization ?? '', title: value.title ?? '', phone: value.phone ?? '' })) ?? [participant()],
    items: record?.items.map(value => ({ clientKey: key(), content: value.content, status: value.status ?? '', dueDate: value.dueDate ?? '', owner: value.owner ?? '' })) ?? [item()],
  })
}

function move<T>(values: T[], index: number, direction: -1 | 1) {
  const target = index + direction
  if (target < 0 || target >= values.length) return
  ;[values[index], values[target]] = [values[target], values[index]]
}

function submit() {
  if (!form.projectId || !form.meetingDate || !form.location.trim()) {
    ElMessage.warning('请选择项目、会议日期并填写会议地点。')
    return
  }
  if (form.participants.length === 0 || form.participants.some(value => !value.name.trim())) {
    ElMessage.warning('请至少填写一位参会人员，且姓名不能为空。')
    return
  }
  if (form.items.length === 0 || form.items.some(value => !value.content.trim())) {
    ElMessage.warning('请至少填写一条会议事项，且需求内容不能为空。')
    return
  }
  const cleanItems = (values: ItemDraft[]) => values.map(value => ({ content: value.content.trim(), status: value.status.trim() || undefined, dueDate: value.dueDate || undefined, owner: value.owner.trim() || undefined }))
  emit('save', {
    projectId: form.projectId,
    meetingDate: form.meetingDate,
    location: form.location.trim(),
    participants: form.participants.map(value => ({ name: value.name.trim(), organization: value.organization.trim() || undefined, title: value.title.trim() || undefined, phone: value.phone.trim() || undefined })),
    items: cleanItems(form.items),
  })
}

watch(() => [visible.value, props.record] as const, ([isVisible]) => { if (isVisible && !props.loading) reset() }, { immediate: true })
watch(() => props.loading, (loading, previous) => { if (previous && !loading && visible.value) reset() })
</script>

<template>
  <el-dialog v-model="visible" :title="title" width="min(980px, calc(100vw - 24px))" :close-on-click-modal="false" :close-on-press-escape="!saving" class="meeting-editor-dialog">
    <div v-loading="loading" class="meeting-editor">
      <el-alert v-if="stale" type="warning" :closable="false" show-icon title="这份会议记录已发生变化，当前输入仍保留。">
        <template #default><el-button text type="warning" :icon="Refresh" @click="emit('reload')">重新载入服务器版本</el-button></template>
      </el-alert>

      <el-form label-position="top">
        <section class="editor-section editor-section--metadata">
          <el-form-item label="项目" required><el-select v-model="form.projectId" filterable placeholder="选择项目"><el-option v-for="project in availableProjects" :key="project.id" :label="`${project.code} · ${project.name}${project.isActive ? '' : '（已停用）'}`" :value="project.id" /></el-select></el-form-item>
          <el-form-item label="会议日期" required><el-date-picker v-model="form.meetingDate" type="date" value-format="YYYY-MM-DD" placeholder="选择日期" /></el-form-item>
          <el-form-item label="会议地点" required><el-input v-model="form.location" maxlength="200" show-word-limit /></el-form-item>
        </section>

        <section class="editor-section">
          <div class="editor-section__head"><div><h3>参会人员</h3><span>{{ form.participants.length }} 人</span></div><el-button :icon="Plus" @click="form.participants.push(participant())">添加人员</el-button></div>
          <div class="editor-grid editor-grid--participants editor-grid--header"><span>姓名</span><span>单位</span><span>职务</span><span>联系电话</span><span>排序</span></div>
          <div v-for="(value, index) in form.participants" :key="value.clientKey" class="editor-grid editor-grid--participants">
            <el-input v-model="value.name" maxlength="100" placeholder="姓名" />
            <el-input v-model="value.organization" maxlength="200" placeholder="单位" />
            <el-input v-model="value.title" maxlength="100" placeholder="职务" />
            <el-input v-model="value.phone" maxlength="50" placeholder="联系电话" />
            <div class="row-actions"><el-tooltip content="上移"><el-button text circle :icon="ArrowUp" :disabled="index === 0" @click="move(form.participants, index, -1)" /></el-tooltip><el-tooltip content="下移"><el-button text circle :icon="ArrowDown" :disabled="index === form.participants.length - 1" @click="move(form.participants, index, 1)" /></el-tooltip><el-tooltip content="删除"><el-button text circle type="danger" :icon="Delete" @click="form.participants.splice(index, 1)" /></el-tooltip></div>
          </div>
        </section>

        <section class="editor-section">
          <div class="editor-section__head"><div><h3>会议内容摘要</h3><span>{{ form.items.length }} 条</span></div><el-button :icon="Plus" @click="form.items.push(item())">添加事项</el-button></div>
          <div class="editor-grid editor-grid--items editor-grid--header"><span>需求内容</span><span>状态</span><span>截止时间</span><span>负责人</span><span>排序</span></div>
          <div v-for="(value, index) in form.items" :key="value.clientKey" class="editor-grid editor-grid--items">
            <el-input v-model="value.content" type="textarea" :autosize="{ minRows: 2, maxRows: 5 }" maxlength="4000" placeholder="填写事项内容" />
            <el-input v-model="value.status" maxlength="100" placeholder="自由填写" />
            <el-date-picker v-model="value.dueDate" type="date" value-format="YYYY-MM-DD" placeholder="可不填" />
            <el-input v-model="value.owner" maxlength="100" placeholder="负责人" />
            <div class="row-actions"><el-tooltip content="上移"><el-button text circle :icon="ArrowUp" :disabled="index === 0" @click="move(form.items, index, -1)" /></el-tooltip><el-tooltip content="下移"><el-button text circle :icon="ArrowDown" :disabled="index === form.items.length - 1" @click="move(form.items, index, 1)" /></el-tooltip><el-tooltip content="删除"><el-button text circle type="danger" :icon="Delete" @click="form.items.splice(index, 1)" /></el-tooltip></div>
          </div>
          <el-empty v-if="form.items.length === 0" description="还没有会议事项" :image-size="52" />
        </section>
      </el-form>
    </div>
    <template #footer><el-button :disabled="saving" @click="visible = false">取消</el-button><el-button type="primary" :loading="saving" :disabled="loading" @click="submit">保存会议记录</el-button></template>
  </el-dialog>
</template>

<style scoped>
.meeting-editor { min-height: 260px; }
.meeting-editor > .el-alert { margin-bottom: 18px; }
.editor-section { padding: 20px 0; border-top: 1px solid var(--line); }
.editor-section:first-of-type { padding-top: 0; border-top: 0; }
.editor-section--metadata { display: grid; grid-template-columns: minmax(220px, 1.3fr) minmax(180px, .8fr) minmax(220px, 1fr); gap: 14px; }
.editor-section__head { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 12px; }
.editor-section__head > div { display: flex; align-items: baseline; gap: 10px; }
.editor-section__head h3 { margin: 0; color: var(--spruce); font-size: 16px; letter-spacing: 0; }
.editor-section__head span { color: var(--muted); font-size: 12px; }
.editor-grid { display: grid; gap: 10px; align-items: center; padding: 8px 0; border-top: 1px solid #edf1ee; }
.editor-grid--header { padding: 7px 0; border-top: 0; color: var(--muted); font-size: 12px; font-weight: 700; }
.editor-grid--participants { grid-template-columns: 1fr 1.35fr 1fr 1fr 112px; }
.editor-grid--items { grid-template-columns: minmax(260px, 2.3fr) minmax(100px, .8fr) 140px minmax(100px, .8fr) 112px; align-items: start; }
.row-actions { display: flex; justify-content: flex-end; min-width: 112px; }
.editor-section :deep(.el-form-item) { margin-bottom: 0; }
.editor-section :deep(.el-select), .editor-section :deep(.el-date-editor) { width: 100%; }
@media (max-width: 760px) {
  .editor-section--metadata { grid-template-columns: 1fr; }
  .editor-grid--header { display: none; }
  .editor-grid--participants, .editor-grid--items { grid-template-columns: 1fr; gap: 8px; padding: 14px 0; }
  .row-actions { justify-content: flex-start; }
}
</style>
