<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Download, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { api, type ApplicantOption, type Project, type WeeklyReport } from '../api'
import { isAdministrator } from '../session'

const rows = ref<WeeklyReport[]>([])
const projects = ref<Project[]>([])
const applicants = ref<ApplicantOption[]>([])
const loading = ref(false)
const exporting = ref(false)
const total = ref(0)
const pageSize = 20
const filters = reactive({ projectId: '', authorId: '', weeks: [] as string[] | null, page: 1 })
const editor = reactive({ visible: false, report: null as WeeklyReport | null, projectId: '', weekStart: '', completedWork: '', nextWeekPlan: '', issues: '' })
const editorTitle = computed(() => editor.report ? `编辑 ${editor.report.authorDisplayName} 的周报` : '新建周报')
const editorProjects = computed(() => projects.value.filter(project => project.isActive || project.id === editor.report?.projectId))

function monday(value: string) { return value && new Date(`${value}T00:00:00`).getDay() === 1 }
function disableNonMonday(date: Date) { return date.getDay() !== 1 }
function weekEnd(value: string) { const date = new Date(`${value}T00:00:00`); date.setDate(date.getDate() + 6); return date.toLocaleDateString('zh-CN', { month: '2-digit', day: '2-digit' }) }
function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) }

async function loadOptions() {
  if (isAdministrator.value) {
    const [projectResult, applicantResult] = await Promise.all([api.listProjects({ page: 1, pageSize: 100 }), api.listApplicants({ page: 1, pageSize: 100 })])
    projects.value = projectResult.items
    applicants.value = applicantResult.items
  } else projects.value = await api.listAvailableProjects()
}

async function load() {
  loading.value = true
  try {
    const common = { projectId: filters.projectId || undefined, weekFrom: filters.weeks?.[0] || undefined, weekTo: filters.weeks?.[1] || undefined, page: filters.page, pageSize }
    const result = isAdministrator.value
      ? await api.listAdminWeeklyReports({ ...common, authorId: filters.authorId || undefined })
      : await api.listWeeklyReports(common)
    rows.value = result.items
    total.value = result.total
  } catch (error) { ElMessage.error(api.message(error, '加载周报失败。')) }
  finally { loading.value = false }
}

function applyFilters() { filters.page = 1; load() }
function openCreate() { Object.assign(editor, { visible: true, report: null, projectId: '', weekStart: '', completedWork: '', nextWeekPlan: '', issues: '' }) }
function openEdit(report: WeeklyReport) { Object.assign(editor, { visible: true, report, projectId: report.projectId, weekStart: report.weekStart, completedWork: report.completedWork, nextWeekPlan: report.nextWeekPlan, issues: report.issues ?? '' }) }

async function exportReports() {
  exporting.value = true
  try {
    const common = { projectId: filters.projectId || undefined, weekFrom: filters.weeks?.[0] || undefined, weekTo: filters.weeks?.[1] || undefined }
    const result = isAdministrator.value
      ? await api.exportAdminWeeklyReports({ ...common, authorId: filters.authorId || undefined })
      : await api.exportWeeklyReports(common)
    const url = URL.createObjectURL(result.blob)
    const link = document.createElement('a')
    link.href = url
    link.download = result.fileName
    link.click()
    URL.revokeObjectURL(url)
    ElMessage.success('周报 Excel 已导出。')
  } catch (error) { ElMessage.error(api.message(error, '导出周报失败。')) }
  finally { exporting.value = false }
}

async function save() {
  if (!editor.projectId || !monday(editor.weekStart) || !editor.completedWork.trim() || !editor.nextWeekPlan.trim()) {
    ElMessage.warning('请选择项目和周一日期，并填写本周完成情况、下周计划。')
    return
  }
  loading.value = true
  try {
    const body = { projectId: editor.projectId, weekStart: editor.weekStart, completedWork: editor.completedWork.trim(), nextWeekPlan: editor.nextWeekPlan.trim(), issues: editor.issues.trim() || undefined }
    if (editor.report) await api.updateWeeklyReport(editor.report.id, { ...body, concurrencyToken: editor.report.concurrencyToken })
    else await api.createWeeklyReport(body)
    editor.visible = false
    ElMessage.success('周报已保存。')
    await load()
  } catch (error) { ElMessage.error(api.message(error, '保存周报失败。')) }
  finally { loading.value = false }
}

onMounted(async () => { try { await loadOptions() } catch (error) { ElMessage.error(api.message(error, '加载筛选项失败。')) }; await load() })
</script>

<template>
  <section>
    <header class="page-header"><div><p class="eyebrow">WEEKLY PROJECT LOG</p><h1>项目周报</h1><p>按项目记录每周完成情况、下周计划和需要协助的问题。</p></div><div class="page-actions"><el-button :icon="Download" :loading="exporting" @click="exportReports">导出 Excel</el-button><el-tooltip content="刷新周报"><el-button circle :icon="Refresh" @click="load" /></el-tooltip><el-button type="primary" :icon="Plus" @click="openCreate">新建周报</el-button></div></header>
    <div class="filter-bar">
      <el-select v-model="filters.projectId" clearable filterable placeholder="全部项目" @change="applyFilters"><el-option v-for="project in projects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select>
      <el-select v-if="isAdministrator" v-model="filters.authorId" clearable filterable placeholder="全部用户" @change="applyFilters"><el-option v-for="applicant in applicants" :key="applicant.id" :label="`${applicant.displayName} · ${applicant.phoneNumber}`" :value="applicant.id" /></el-select>
      <el-date-picker v-model="filters.weeks" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="周开始" end-placeholder="周结束" :disabled-date="disableNonMonday" @change="applyFilters" />
    </div>
    <div class="table-shell desktop-table" v-loading="loading"><el-table :data="rows" empty-text="当前没有周报。">
      <el-table-column label="周" width="150"><template #default="scope"><strong>{{ scope.row.weekStart }}</strong><span> 至 {{ weekEnd(scope.row.weekStart) }}</span></template></el-table-column>
      <el-table-column v-if="isAdministrator" label="用户" min-width="140"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.authorPersonalName || scope.row.authorDisplayName }}</strong><span>{{ scope.row.authorDisplayName }}</span></div></template></el-table-column>
      <el-table-column label="项目" min-width="180"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.projectName }}</strong><span>{{ scope.row.projectCode }}</span></div></template></el-table-column>
      <el-table-column prop="completedWork" label="本周完成" min-width="260" show-overflow-tooltip />
      <el-table-column prop="nextWeekPlan" label="下周计划" min-width="220" show-overflow-tooltip />
      <el-table-column label="最后编辑" width="165"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.lastEditedByDisplayName }}</strong><span>{{ dateTime(scope.row.updatedAt) }}</span></div></template></el-table-column>
      <el-table-column label="操作" width="70" fixed="right"><template #default="scope"><el-tooltip content="编辑周报"><el-button text circle :icon="Edit" @click="openEdit(scope.row)" /></el-tooltip></template></el-table-column>
    </el-table></div>
    <div class="mobile-list" v-loading="loading"><article v-for="item in rows" :key="item.id" class="mobile-record"><div class="mobile-record__head"><div><strong>{{ item.projectName }}</strong><span>{{ item.weekStart }} 至 {{ weekEnd(item.weekStart) }}</span></div><el-button text circle :icon="Edit" @click="openEdit(item)" /></div><p><strong>本周完成：</strong>{{ item.completedWork }}</p><p><strong>下周计划：</strong>{{ item.nextWeekPlan }}</p><p v-if="item.issues"><strong>问题：</strong>{{ item.issues }}</p></article><el-empty v-if="!loading && rows.length === 0" description="当前没有周报" /></div>
    <el-pagination v-if="total > pageSize" class="pagination" v-model:current-page="filters.page" :page-size="pageSize" :total="total" layout="prev, pager, next" @current-change="load" />
    <el-dialog v-model="editor.visible" :title="editorTitle" width="min(640px, calc(100vw - 32px))" :close-on-click-modal="false"><el-form label-position="top">
      <el-form-item label="项目" required><el-select v-model="editor.projectId" filterable><el-option v-for="project in editorProjects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select></el-form-item>
      <el-form-item label="周一日期" required><el-date-picker v-model="editor.weekStart" type="date" value-format="YYYY-MM-DD" placeholder="请选择周一" :disabled-date="disableNonMonday" /></el-form-item>
      <el-form-item label="本周完成情况" required><el-input v-model="editor.completedWork" type="textarea" :rows="5" maxlength="4000" show-word-limit /></el-form-item>
      <el-form-item label="下周计划" required><el-input v-model="editor.nextWeekPlan" type="textarea" :rows="4" maxlength="4000" show-word-limit /></el-form-item>
      <el-form-item label="问题与需协助事项"><el-input v-model="editor.issues" type="textarea" :rows="3" maxlength="4000" show-word-limit /></el-form-item>
    </el-form><template #footer><el-button @click="editor.visible = false">取消</el-button><el-button type="primary" :loading="loading" @click="save">保存周报</el-button></template></el-dialog>
  </section>
</template>
