<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Download, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { api, type ApiProblem, type MeetingRecordDetail, type MeetingRecordListRow, type MeetingRecordPayload, type MeetingRecordProject } from '../api'
import MeetingRecordEditorDialog from '../components/MeetingRecordEditorDialog.vue'
import { isAdministrator } from '../session'

const rows = ref<MeetingRecordListRow[]>([])
const projects = ref<MeetingRecordProject[]>([])
const loading = ref(false)
const detailLoading = ref(false)
const saving = ref(false)
const deletingId = ref('')
const exporting = ref(false)
const total = ref(0)
const pageSize = 20
const filters = reactive({ projectId: '', dates: [] as string[] | null, page: 1 })
const editor = reactive({ visible: false, record: null as MeetingRecordDetail | null, stale: false })
const exportDialog = reactive({ visible: false, projectId: '' })

function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) }

async function loadProjects() {
  projects.value = await api.listMeetingRecordProjects()
}

async function load() {
  loading.value = true
  try {
    const result = await api.listMeetingRecords({ projectId: filters.projectId || undefined, dateFrom: filters.dates?.[0] || undefined, dateTo: filters.dates?.[1] || undefined, page: filters.page, pageSize })
    rows.value = result.items
    total.value = result.total
  } catch (error) { ElMessage.error(api.message(error, '加载会议记录失败。')) }
  finally { loading.value = false }
}

function applyFilters() { filters.page = 1; load() }
function openCreate() { Object.assign(editor, { visible: true, record: null, stale: false }) }

async function openEdit(row: MeetingRecordListRow) {
  Object.assign(editor, { visible: true, record: null, stale: false })
  detailLoading.value = true
  try { editor.record = await api.getMeetingRecord(row.id) }
  catch (error) { editor.visible = false; ElMessage.error(api.message(error, '加载会议记录详情失败。')) }
  finally { detailLoading.value = false }
}

async function reloadEditor() {
  if (!editor.record) return
  detailLoading.value = true
  try { editor.record = await api.getMeetingRecord(editor.record.id); editor.stale = false; ElMessage.success('已重新载入服务器版本。') }
  catch (error) { ElMessage.error(api.message(error, '重新载入会议记录失败。')) }
  finally { detailLoading.value = false }
}

async function save(payload: MeetingRecordPayload) {
  saving.value = true
  try {
    if (editor.record) await api.updateMeetingRecord(editor.record.id, { ...payload, concurrencyToken: editor.record.concurrencyToken })
    else await api.createMeetingRecord(payload)
    editor.visible = false
    ElMessage.success('会议记录已保存。')
    await Promise.all([load(), loadProjects()])
  } catch (error) {
    if ((error as ApiProblem).code === 'MEETING_RECORD_STALE') editor.stale = true
    ElMessage.error(api.message(error, '保存会议记录失败。'))
  } finally { saving.value = false }
}

async function remove(row: MeetingRecordListRow) {
  try { await ElMessageBox.confirm(`确认删除 ${row.meetingDate} 的会议记录？删除后普通列表和导出均不再显示。`, '删除会议记录', { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' }) }
  catch { return }
  deletingId.value = row.id
  try { await api.deleteMeetingRecord(row.id, row.concurrencyToken); ElMessage.success('会议记录已删除。'); await Promise.all([load(), loadProjects()]) }
  catch (error) { ElMessage.error(api.message(error, '删除会议记录失败。')) }
  finally { deletingId.value = '' }
}

function openExport() { exportDialog.projectId = filters.projectId; exportDialog.visible = true }

async function exportRecords() {
  if (!exportDialog.projectId) { ElMessage.warning('请选择要导出的项目。'); return }
  exporting.value = true
  try {
    const result = await api.exportMeetingRecords(exportDialog.projectId)
    const url = URL.createObjectURL(result.blob)
    const link = document.createElement('a')
    link.href = url
    link.download = result.fileName
    link.click()
    URL.revokeObjectURL(url)
    exportDialog.visible = false
    ElMessage.success('项目全部会议记录已导出。')
  } catch (error) { ElMessage.error(api.message(error, '导出会议记录失败。')) }
  finally { exporting.value = false }
}

onMounted(async () => {
  try { await loadProjects() } catch (error) { ElMessage.error(api.message(error, '加载项目失败。')) }
  await load()
})
</script>

<template>
  <section>
    <header class="page-header"><div><p class="eyebrow">PROJECT MEETING LOG</p><h1>会议记录</h1><p>按项目沉淀会议参与人和会议内容摘要。</p></div><div class="page-actions"><el-button :icon="Download" @click="openExport">按项目导出</el-button><el-tooltip content="刷新会议记录"><el-button circle :icon="Refresh" @click="load" /></el-tooltip><el-button type="primary" :icon="Plus" @click="openCreate">新建会议记录</el-button></div></header>
    <div class="filter-bar">
      <el-select v-model="filters.projectId" clearable filterable placeholder="全部项目" @change="applyFilters"><el-option v-for="project in projects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select>
      <el-date-picker v-model="filters.dates" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="会议开始" end-placeholder="会议结束" @change="applyFilters" />
    </div>

    <div class="table-shell desktop-table" v-loading="loading"><el-table :data="rows" empty-text="当前没有会议记录。">
      <el-table-column prop="meetingDate" label="会议日期" width="125" />
      <el-table-column label="项目" min-width="190"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.projectName }}</strong><span>{{ scope.row.projectCode }}</span></div></template></el-table-column>
      <el-table-column prop="location" label="地点" min-width="150" show-overflow-tooltip />
      <el-table-column label="内容概览" min-width="260"><template #default="scope"><div class="meeting-summary"><span>{{ scope.row.firstItemContent || '暂无摘要' }}</span><small>{{ scope.row.participantCount }} 人 · {{ scope.row.itemCount }} 条事项</small></div></template></el-table-column>
      <el-table-column label="最后编辑" width="165"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.lastEditedByDisplayName }}</strong><span>{{ dateTime(scope.row.updatedAt) }}</span></div></template></el-table-column>
      <el-table-column label="操作" :width="isAdministrator ? 110 : 64" fixed="right"><template #default="scope"><el-tooltip content="编辑会议记录"><el-button text circle :icon="Edit" @click="openEdit(scope.row)" /></el-tooltip><el-tooltip v-if="isAdministrator" content="删除会议记录"><el-button text circle type="danger" :icon="Delete" :loading="deletingId === scope.row.id" @click="remove(scope.row)" /></el-tooltip></template></el-table-column>
    </el-table></div>

    <div class="mobile-list" v-loading="loading"><article v-for="item in rows" :key="item.id" class="mobile-record"><div class="mobile-record__head"><div><strong>{{ item.projectName }}</strong><span>{{ item.meetingDate }} · {{ item.location }}</span></div><div><el-button text circle :icon="Edit" @click="openEdit(item)" /><el-button v-if="isAdministrator" text circle type="danger" :icon="Delete" :loading="deletingId === item.id" @click="remove(item)" /></div></div><p>{{ item.firstItemContent || '暂无内容摘要' }}</p><p class="meeting-counts">{{ item.participantCount }} 人 · {{ item.itemCount }} 条事项</p></article><el-empty v-if="!loading && rows.length === 0" description="当前没有会议记录" /></div>
    <el-pagination v-if="total > pageSize" class="pagination" v-model:current-page="filters.page" :page-size="pageSize" :total="total" layout="prev, pager, next" @current-change="load" />

    <MeetingRecordEditorDialog v-model="editor.visible" :record="editor.record" :projects="projects" :saving="saving" :loading="detailLoading" :stale="editor.stale" @save="save" @reload="reloadEditor" />

    <el-dialog v-model="exportDialog.visible" title="导出项目会议记录" width="min(480px, calc(100vw - 32px))" :close-on-click-modal="false">
      <el-form label-position="top"><el-form-item label="项目" required><el-select v-model="exportDialog.projectId" filterable placeholder="选择项目"><el-option v-for="project in projects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select></el-form-item></el-form>
      <p class="export-note">将导出该项目当前全部会议记录，不受页面日期筛选和分页影响；每次会议对应一个工作表。</p>
      <template #footer><el-button @click="exportDialog.visible = false">取消</el-button><el-button type="primary" :icon="Download" :loading="exporting" @click="exportRecords">导出 Excel</el-button></template>
    </el-dialog>
  </section>
</template>

<style scoped>
.meeting-summary { display: flex; min-width: 0; flex-direction: column; gap: 4px; }
.meeting-summary > span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.meeting-summary small, .meeting-counts, .export-note { color: var(--muted); }
.meeting-counts { margin-top: 8px !important; font-size: 12px; }
.export-note { margin: 2px 0 0; font-size: 13px; line-height: 1.65; }
</style>
