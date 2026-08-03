<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Edit, Plus, Refresh, SwitchButton } from '@element-plus/icons-vue'
import { api, type Project } from '../api'

const loading = ref(false)
const rows = ref<Project[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const keyword = ref('')
const activeFilter = ref<'all' | 'active' | 'inactive'>('all')
const dialogOpen = ref(false)
const editing = ref<Project | null>(null)
const form = reactive({ code: '', name: '', description: '' })

async function load() {
  loading.value = true
  try {
    const result = await api.listProjects({ isActive: activeFilter.value === 'all' ? undefined : activeFilter.value === 'active', keyword: keyword.value.trim(), page: page.value, pageSize })
    rows.value = result.items
    total.value = result.total
  } catch (error) {
    ElMessage.error(api.message(error, '加载项目失败。'))
  } finally {
    loading.value = false
  }
}

function applyFilters() { page.value = 1; load() }
function openCreate() { editing.value = null; form.code = ''; form.name = ''; form.description = ''; dialogOpen.value = true }
function openEdit(project: Project) { editing.value = project; form.code = project.code; form.name = project.name; form.description = project.description ?? ''; dialogOpen.value = true }

async function save() {
  if (!form.code.trim() || !form.name.trim()) { ElMessage.error('项目编码和名称不能为空。'); return }
  loading.value = true
  try {
    if (editing.value) await api.updateProject(editing.value.id, { name: form.name.trim(), description: form.description.trim() || undefined, concurrencyToken: editing.value.concurrencyToken })
    else await api.createProject({ code: form.code.trim(), name: form.name.trim(), description: form.description.trim() || undefined })
    dialogOpen.value = false
    ElMessage.success(editing.value ? '项目已更新。' : '项目已创建。')
    await load()
  } catch (error) {
    ElMessage.error(api.message(error, '保存项目失败。'))
    if (editing.value) await load()
  } finally {
    loading.value = false
  }
}

async function toggleActive(project: Project) {
  const active = !project.isActive
  try {
    await ElMessageBox.confirm(active ? `确认启用项目“${project.name}”？` : `停用后不能用于新建报销，确认停用“${project.name}”？`, active ? '启用项目' : '停用项目', { confirmButtonText: active ? '确认启用' : '确认停用', cancelButtonText: '取消', type: active ? 'info' : 'warning' })
    await api.setProjectActive(project.id, active)
    ElMessage.success(active ? '项目已启用。' : '项目已停用。')
    await load()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error(api.message(error, '更新项目状态失败。'))
  }
}

onMounted(load)
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">PROJECT DIRECTORY</p><h1>项目管理</h1><p>所有用户可选择启用项目；已有引用的项目只停用，不删除。</p></div>
      <el-button type="primary" :icon="Plus" @click="openCreate">新建项目</el-button>
    </header>
    <div class="filter-bar filter-bar--split">
      <div class="filter-cluster">
        <el-input v-model="keyword" clearable placeholder="搜索编码或名称" @keyup.enter="applyFilters" @clear="applyFilters" />
        <el-radio-group v-model="activeFilter" @change="applyFilters"><el-radio-button value="all">全部</el-radio-button><el-radio-button value="active">启用</el-radio-button><el-radio-button value="inactive">停用</el-radio-button></el-radio-group>
      </div>
      <el-tooltip content="刷新项目"><el-button circle :icon="Refresh" aria-label="刷新项目" @click="load" /></el-tooltip>
    </div>
    <div class="table-shell desktop-table" v-loading="loading">
      <el-table :data="rows" empty-text="还没有项目。">
        <el-table-column label="项目" min-width="230"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.name }}</strong><span>{{ scope.row.code }}</span></div></template></el-table-column>
        <el-table-column prop="description" label="说明" min-width="260" show-overflow-tooltip />
        <el-table-column label="状态" width="100"><template #default="scope"><el-tag :type="scope.row.isActive ? 'success' : 'info'" effect="plain">{{ scope.row.isActive ? '启用' : '停用' }}</el-tag></template></el-table-column>
        <el-table-column label="更新时间" width="190"><template #default="scope">{{ scope.row.updatedAt ? new Date(scope.row.updatedAt).toLocaleString('zh-CN', { hour12: false }) : '-' }}</template></el-table-column>
        <el-table-column label="操作" width="120" fixed="right"><template #default="scope"><el-tooltip content="编辑项目"><el-button circle text :icon="Edit" aria-label="编辑项目" @click="openEdit(scope.row)" /></el-tooltip><el-tooltip :content="scope.row.isActive ? '停用项目' : '启用项目'"><el-button circle text :type="scope.row.isActive ? 'danger' : 'success'" :icon="SwitchButton" :aria-label="scope.row.isActive ? '停用项目' : '启用项目'" @click="toggleActive(scope.row)" /></el-tooltip></template></el-table-column>
      </el-table>
    </div>
    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record">
        <div class="mobile-record__head"><div><strong>{{ item.name }}</strong><span>{{ item.code }}</span></div><el-tag :type="item.isActive ? 'success' : 'info'" effect="plain">{{ item.isActive ? '启用' : '停用' }}</el-tag></div>
        <p>{{ item.description || '暂无说明' }}</p>
        <div class="mobile-record__actions"><el-button :icon="Edit" @click="openEdit(item)">编辑</el-button><el-button :type="item.isActive ? 'danger' : 'success'" plain :icon="SwitchButton" @click="toggleActive(item)">{{ item.isActive ? '停用' : '启用' }}</el-button></div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="还没有项目" />
    </div>
    <el-pagination v-if="total > pageSize" class="pagination" v-model:current-page="page" :page-size="pageSize" :total="total" layout="prev, pager, next" @current-change="load" />

    <el-dialog v-model="dialogOpen" :title="editing ? '编辑项目' : '新建项目'" width="min(520px, calc(100vw - 32px))">
      <el-form label-position="top">
        <el-form-item label="项目编码" required><el-input v-model="form.code" maxlength="50" :disabled="!!editing" placeholder="例如：PRJ-2026-001" /></el-form-item>
        <el-form-item label="项目名称" required><el-input v-model="form.name" maxlength="200" placeholder="项目名称" /></el-form-item>
        <el-form-item label="项目说明"><el-input v-model="form.description" type="textarea" :rows="4" maxlength="1000" show-word-limit /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogOpen = false">取消</el-button><el-button type="primary" :loading="loading" @click="save">保存项目</el-button></template>
    </el-dialog>
  </section>
</template>
