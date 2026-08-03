<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Edit, Plus, Refresh, View } from '@element-plus/icons-vue'
import { api, type ClaimListRow, type ClaimStatus, type Project } from '../api'
import ClaimDetailDrawer from '../components/ClaimDetailDrawer.vue'
import ClaimEditorDialog from '../components/ClaimEditorDialog.vue'

const loading = ref(false)
const rows = ref<ClaimListRow[]>([])
const projects = ref<Project[]>([])
const total = ref(0)
const editorOpen = ref(false)
const editorClaimId = ref<string | null>(null)
const detailOpen = ref(false)
const detailClaimId = ref<string | null>(null)
const filters = reactive<{ projectId: string; status: '' | ClaimStatus; page: number; pageSize: number }>({ projectId: '', status: '', page: 1, pageSize: 20 })

const statusLabels: Record<string, string> = { Draft: '草稿', Submitted: '待审批', Approved: '已批准', Rejected: '已驳回', Cancelled: '已作废' }
const payoutLabels: Record<string, string> = { NotApplicable: '无需发放', Pending: '待发放', Paid: '已发放' }

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) }
function canEdit(row: ClaimListRow) { return ['Draft', 'Submitted', 'Rejected'].includes(row.status) }
function cancelLabel(row: ClaimListRow) { return row.status === 'Submitted' ? '撤回' : '删除' }

async function loadProjects() {
  try { projects.value = await api.listMyProjects() }
  catch (error) { ElMessage.error(api.message(error, '加载项目失败。')) }
}

async function load() {
  loading.value = true
  try {
    const result = await api.listClaims({ projectId: filters.projectId, status: filters.status || undefined, page: filters.page, pageSize: filters.pageSize })
    rows.value = result.items
    total.value = result.total
  } catch (error) {
    ElMessage.error(api.message(error, '加载报销列表失败。'))
  } finally {
    loading.value = false
  }
}

function applyFilters() { filters.page = 1; load() }
function openCreate() { editorClaimId.value = null; editorOpen.value = true }
function openEdit(row: ClaimListRow) { editorClaimId.value = row.id; editorOpen.value = true }
function openDetail(row: ClaimListRow) { detailClaimId.value = row.id; detailOpen.value = true }

async function afterSaved() {
  await Promise.all([loadProjects(), load()])
}

async function cancelClaim(row: ClaimListRow) {
  const action = cancelLabel(row)
  try {
    await ElMessageBox.confirm(
      action === '撤回' ? `确认撤回报销“${row.claimNumber}”？撤回后记录将作废。` : `确认删除报销“${row.claimNumber}”？删除后记录将作废。`,
      `${action}报销`,
      { confirmButtonText: `确认${action}`, cancelButtonText: '取消', type: 'warning' },
    )
    await api.cancelClaim(row.id, { expectedCurrentVersionId: row.currentVersionId, concurrencyToken: row.concurrencyToken })
    ElMessage.success(`报销已${action === '撤回' ? '撤回' : '删除'}。`)
    await load()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(api.message(error, `${action}报销失败。`))
      if ((error as { status?: number }).status === 409) await load()
    }
  }
}

onMounted(async () => { await Promise.all([loadProjects(), load()]) })
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">MY REIMBURSEMENTS</p><h1>我的报销</h1><p>按项目查看当前版本、审批状态和发放状态。</p></div>
      <div class="page-actions"><el-tooltip content="刷新列表"><el-button circle :icon="Refresh" aria-label="刷新列表" @click="load" /></el-tooltip><el-button type="primary" :icon="Plus" @click="openCreate">新增报销</el-button></div>
    </header>
    <div class="filter-bar">
      <el-select v-model="filters.projectId" clearable filterable placeholder="全部项目" @change="applyFilters"><el-option v-for="project in projects" :key="project.id" :label="`${project.code} · ${project.name}`" :value="project.id" /></el-select>
      <el-select v-model="filters.status" clearable placeholder="全部报销状态" @change="applyFilters"><el-option v-for="status in ['Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled']" :key="status" :label="statusLabels[status]" :value="status" /></el-select>
    </div>
    <div class="table-shell desktop-table" v-loading="loading">
      <el-table :data="rows" empty-text="当前筛选条件下没有报销。">
        <el-table-column label="项目" min-width="190"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.projectName }}</strong><span>{{ scope.row.projectCode }}</span></div></template></el-table-column>
        <el-table-column label="报销" min-width="205"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.claimNumber }}</strong><span>v{{ scope.row.versionNumber }} · {{ scope.row.type === 'Travel' ? '差旅' : '单据' }}</span></div></template></el-table-column>
        <el-table-column prop="description" label="说明" min-width="180" show-overflow-tooltip />
        <el-table-column label="金额" width="125" align="right"><template #default="scope">{{ money(scope.row.totalAmount) }}</template></el-table-column>
        <el-table-column label="报销状态" width="105"><template #default="scope"><el-tag :type="scope.row.status === 'Approved' ? 'success' : scope.row.status === 'Rejected' ? 'danger' : scope.row.status === 'Submitted' ? 'warning' : 'info'" effect="plain">{{ statusLabels[scope.row.status] }}</el-tag></template></el-table-column>
        <el-table-column label="发放状态" width="105"><template #default="scope"><el-tag :type="scope.row.payoutStatus === 'Paid' ? 'success' : scope.row.payoutStatus === 'Pending' ? 'warning' : 'info'" effect="plain">{{ payoutLabels[scope.row.payoutStatus] }}</el-tag></template></el-table-column>
        <el-table-column label="更新" width="120"><template #default="scope">{{ dateTime(scope.row.updatedAt) }}</template></el-table-column>
        <el-table-column label="操作" width="132" fixed="right"><template #default="scope"><el-tooltip content="查看详情"><el-button text circle :icon="View" aria-label="查看详情" @click="openDetail(scope.row)" /></el-tooltip><el-tooltip v-if="canEdit(scope.row)" content="编辑报销"><el-button text circle :icon="Edit" aria-label="编辑报销" @click="openEdit(scope.row)" /></el-tooltip><el-tooltip v-if="canEdit(scope.row)" :content="`${cancelLabel(scope.row)}报销`"><el-button text circle type="danger" :icon="Delete" :aria-label="`${cancelLabel(scope.row)}报销`" @click="cancelClaim(scope.row)" /></el-tooltip></template></el-table-column>
      </el-table>
    </div>
    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record claim-mobile-record">
        <div class="mobile-record__head"><div><strong>{{ item.projectName }}</strong><span>{{ item.projectCode }} · {{ item.claimNumber }} · v{{ item.versionNumber }}</span></div><strong>{{ money(item.totalAmount) }}</strong></div>
        <p>{{ item.description || '暂无报销说明' }}</p>
        <div class="claim-mobile-status"><el-tag effect="plain">{{ statusLabels[item.status] }}</el-tag><el-tag :type="item.payoutStatus === 'Paid' ? 'success' : item.payoutStatus === 'Pending' ? 'warning' : 'info'" effect="plain">{{ payoutLabels[item.payoutStatus] }}</el-tag></div>
        <div class="mobile-record__actions"><el-button :icon="View" @click="openDetail(item)">查看</el-button><el-button v-if="canEdit(item)" :icon="Edit" @click="openEdit(item)">编辑</el-button><el-button v-if="canEdit(item)" type="danger" plain :icon="Delete" @click="cancelClaim(item)">{{ cancelLabel(item) }}</el-button></div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="当前筛选条件下没有报销" />
    </div>
    <el-pagination v-if="total > filters.pageSize" class="pagination" v-model:current-page="filters.page" :page-size="filters.pageSize" :total="total" layout="prev, pager, next" @current-change="load" />

    <ClaimEditorDialog v-model="editorOpen" :claim-id="editorClaimId" @saved="afterSaved" />
    <ClaimDetailDrawer v-model="detailOpen" :claim-id="detailClaimId" />
  </section>
</template>
