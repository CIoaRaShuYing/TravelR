<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import { api, type RegistrationRequest, type RegistrationRequestStatus } from '../api'

const loading = ref(false)
const rows = ref<RegistrationRequest[]>([])
const total = ref(0)
const status = ref<RegistrationRequestStatus>('Pending')
const page = ref(1)
const pageSize = 20
const dialogOpen = ref(false)
const decision = ref<'approve' | 'reject'>('approve')
const selected = ref<RegistrationRequest | null>(null)
const statusLabels: Record<RegistrationRequestStatus, string> = { Pending: '待审批', Approved: '已批准', Rejected: '已拒绝' }

function dateTime(value?: string | null) { return value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '-' }
function registrationStatusLabel(value: RegistrationRequestStatus) { return statusLabels[value] }

async function load() {
  loading.value = true
  try {
    const result = await api.listRegistrationRequests({ status: status.value, page: page.value, pageSize })
    rows.value = result.items
    total.value = result.total
  } catch (error) {
    ElMessage.error(api.message(error, '加载注册申请失败。'))
  } finally {
    loading.value = false
  }
}

function openDecision(item: RegistrationRequest, action: 'approve' | 'reject') {
  selected.value = item
  decision.value = action
  dialogOpen.value = true
}

async function submitDecision() {
  if (!selected.value) return
  loading.value = true
  try {
    const request = { concurrencyToken: selected.value.concurrencyToken }
    if (decision.value === 'approve') await api.approveRegistration(selected.value.id, request)
    else await api.rejectRegistration(selected.value.id, request)
    dialogOpen.value = false
    ElMessage.success(decision.value === 'approve' ? '注册申请已批准。' : '注册申请已拒绝。')
    await load()
  } catch (error) {
    ElMessage.error(api.message(error, '处理注册申请失败。'))
    await load()
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">ACCESS REVIEW</p><h1>注册审批</h1><p>批准后账户立即启用；拒绝后保留申请记录和处理时间。</p></div>
      <el-tooltip content="刷新申请"><el-button circle :icon="Refresh" aria-label="刷新申请" @click="load" /></el-tooltip>
    </header>
    <div class="filter-bar filter-bar--split">
      <el-radio-group v-model="status" @change="page = 1; load()">
        <el-radio-button value="Pending">待审批</el-radio-button>
        <el-radio-button value="Approved">已批准</el-radio-button>
        <el-radio-button value="Rejected">已拒绝</el-radio-button>
      </el-radio-group>
      <span class="result-count">{{ total }} 条记录</span>
    </div>
    <div class="table-shell desktop-table" v-loading="loading">
      <el-table :data="rows" empty-text="当前没有注册申请。">
        <el-table-column label="申请人" min-width="180"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.displayName }}</strong><span>{{ scope.row.phoneNumber }}</span></div></template></el-table-column>
        <el-table-column label="提交时间" width="190"><template #default="scope">{{ dateTime(scope.row.createdAt) }}</template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="scope"><el-tag :type="scope.row.status === 'Approved' ? 'success' : scope.row.status === 'Rejected' ? 'danger' : 'warning'" effect="plain">{{ registrationStatusLabel(scope.row.status) }}</el-tag></template></el-table-column>
        <el-table-column label="处理时间" width="190"><template #default="scope">{{ dateTime(scope.row.reviewedAt) }}</template></el-table-column>
        <el-table-column v-if="status === 'Pending'" label="操作" width="170" fixed="right"><template #default="scope"><el-button size="small" type="primary" @click="openDecision(scope.row, 'approve')">批准</el-button><el-button size="small" type="danger" plain @click="openDecision(scope.row, 'reject')">拒绝</el-button></template></el-table-column>
      </el-table>
    </div>
    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record">
        <div class="mobile-record__head"><div><strong>{{ item.displayName }}</strong><span>{{ item.phoneNumber }}</span></div><el-tag :type="item.status === 'Approved' ? 'success' : item.status === 'Rejected' ? 'danger' : 'warning'" effect="plain">{{ statusLabels[item.status] }}</el-tag></div>
        <dl><div><dt>提交</dt><dd>{{ dateTime(item.createdAt) }}</dd></div><div v-if="item.reviewedAt"><dt>处理</dt><dd>{{ dateTime(item.reviewedAt) }}</dd></div></dl>
        <div v-if="status === 'Pending'" class="mobile-record__actions"><el-button type="primary" @click="openDecision(item, 'approve')">批准</el-button><el-button type="danger" plain @click="openDecision(item, 'reject')">拒绝</el-button></div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="当前没有注册申请" />
    </div>
    <el-pagination v-if="total > pageSize" class="pagination" v-model:current-page="page" :page-size="pageSize" :total="total" layout="prev, pager, next" @current-change="load" />

    <el-dialog v-model="dialogOpen" :title="decision === 'approve' ? '批准注册申请' : '拒绝注册申请'" width="min(460px, calc(100vw - 32px))">
      <div v-if="selected" class="dialog-subject"><strong>{{ selected.displayName }}</strong><span>{{ selected.phoneNumber }}</span></div>
      <p class="decision-note">{{ decision === 'approve' ? '确认后将创建并启用该用户账户。' : '确认后该申请将标记为已拒绝。' }}</p>
      <template #footer><el-button @click="dialogOpen = false">取消</el-button><el-button :type="decision === 'approve' ? 'primary' : 'danger'" :loading="loading" @click="submitDecision">{{ decision === 'approve' ? '确认批准' : '确认拒绝' }}</el-button></template>
    </el-dialog>
  </section>
</template>
