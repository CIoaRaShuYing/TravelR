<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Key, Refresh, SwitchButton } from '@element-plus/icons-vue'
import { api, type AdminUser } from '../api'
import { session } from '../session'

type ActiveFilter = 'all' | 'active' | 'inactive'

const SUPER_ADMIN_PHONE_NUMBER = '13730614340'
const rows = ref<AdminUser[]>([])
const loading = ref(false)
const total = ref(0)
const pageSize = 20
const filters = reactive<{ keyword: string; active: ActiveFilter; page: number }>({ keyword: '', active: 'all', page: 1 })
const resetDialog = reactive({ visible: false, loading: false, target: null as AdminUser | null, newPassword: '', confirmPassword: '' })

const roleLabels: Record<string, string> = { Applicant: '申请人', Administrator: '管理员' }

function roleLabel(role: string) { return roleLabels[role] ?? role }
function statusType(active: boolean) { return active ? 'success' : 'info' }
function isAdministrator(user: AdminUser) { return user.roles.includes('Administrator') }
function isSuperAdministrator(user: AdminUser) { return user.phoneNumber === SUPER_ADMIN_PHONE_NUMBER }
function canRevokeAdministrator(user: AdminUser) { return isAdministrator(user) && user.id !== session.value?.user.id && !isSuperAdministrator(user) }

async function load() {
  loading.value = true
  try {
    const result = await api.listUsers({
      keyword: filters.keyword.trim() || undefined,
      isActive: filters.active === 'all' ? undefined : filters.active === 'active',
      page: filters.page,
      pageSize,
    })
    rows.value = result.items
    total.value = result.total
  } catch (error) {
    ElMessage.error(api.message(error, '加载用户列表失败。'))
  } finally {
    loading.value = false
  }
}

function applyFilters() {
  filters.page = 1
  load()
}

async function toggleActive(user: AdminUser) {
  const active = !user.isActive
  try {
    await ElMessageBox.confirm(
      active ? `确认启用“${user.displayName}”账户？` : `停用后该账户将不能登录，确认停用“${user.displayName}”？`,
      active ? '启用用户' : '停用用户',
      { confirmButtonText: active ? '确认启用' : '确认停用', cancelButtonText: '取消', type: active ? 'info' : 'warning' },
    )
    loading.value = true
    await api.setUserActive(user.id, active)
    ElMessage.success(active ? '用户已启用。' : '用户已停用。')
    await load()
  } catch (error) {
    if (error === 'cancel' || error === 'close') return
    ElMessage.error(api.message(error, '更新用户状态失败。'))
  } finally {
    loading.value = false
  }
}

async function toggleAdministrator(user: AdminUser) {
  const grant = !isAdministrator(user)
  if (!grant && isSuperAdministrator(user)) return
  try {
    await ElMessageBox.confirm(
      grant ? `确认将“${user.displayName}”设为管理员？` : `取消后“${user.displayName}”将不能进入管理功能，确认取消管理员？`,
      grant ? '设为管理员' : '取消管理员',
      { confirmButtonText: grant ? '确认设置' : '确认取消', cancelButtonText: '取消', type: grant ? 'info' : 'warning' },
    )
    loading.value = true
    await api.setUserAdministrator(user.id, grant)
    ElMessage.success(grant ? '管理员角色已授予。' : '管理员角色已取消。')
    await load()
  } catch (error) {
    if (error === 'cancel' || error === 'close') return
    ElMessage.error(api.message(error, '更新管理员角色失败。'))
  } finally {
    loading.value = false
  }
}

function openResetPassword(user: AdminUser) {
  resetDialog.target = user
  resetDialog.newPassword = ''
  resetDialog.confirmPassword = ''
  resetDialog.visible = true
}

async function resetPassword() {
  if (!resetDialog.target) return
  if (!resetDialog.newPassword || !resetDialog.confirmPassword) {
    ElMessage.warning('请填写新密码和确认密码。')
    return
  }
  if (resetDialog.newPassword !== resetDialog.confirmPassword) {
    ElMessage.warning('两次输入的新密码不一致。')
    return
  }
  resetDialog.loading = true
  try {
    await api.resetUserPassword(resetDialog.target.id, resetDialog.newPassword)
    resetDialog.visible = false
    ElMessage.success('密码重置成功，请通过安全渠道通知目标用户。')
  } catch (error) {
    ElMessage.error(api.message(error, '重置密码失败。'))
  } finally {
    resetDialog.loading = false
  }
}

onMounted(load)
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">USER DIRECTORY</p><h1>用户中心</h1><p>管理正式用户账户、角色和密码；所有安全变更都会记录审计。</p></div>
      <el-tooltip content="刷新用户列表"><el-button circle :icon="Refresh" aria-label="刷新用户列表" @click="load" /></el-tooltip>
    </header>

    <div class="filter-bar filter-bar--split">
      <div class="filter-cluster">
        <el-input v-model="filters.keyword" clearable placeholder="按姓名或手机号搜索" @keyup.enter="applyFilters" @clear="applyFilters" />
        <el-radio-group v-model="filters.active" @change="applyFilters">
          <el-radio-button value="all">全部</el-radio-button>
          <el-radio-button value="active">启用</el-radio-button>
          <el-radio-button value="inactive">停用</el-radio-button>
        </el-radio-group>
        <el-button type="primary" @click="applyFilters">搜索</el-button>
      </div>
      <span class="result-count">共 {{ total }} 个正式用户</span>
    </div>

    <div class="table-shell desktop-table" v-loading="loading">
      <el-table :data="rows" empty-text="当前没有正式用户。">
        <el-table-column label="用户" min-width="180"><template #default="scope"><div class="primary-cell"><strong>{{ scope.row.displayName }}</strong><span>{{ scope.row.id.slice(0, 8) }}</span></div></template></el-table-column>
        <el-table-column prop="phoneNumber" label="手机号" min-width="150" />
        <el-table-column label="角色" min-width="190"><template #default="scope"><div class="user-role-tags"><el-tag v-for="role in scope.row.roles" :key="role" effect="plain">{{ roleLabel(role) }}</el-tag><el-tag v-if="isSuperAdministrator(scope.row)" type="warning" effect="plain">超级管理员</el-tag></div></template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="scope"><el-tag :type="statusType(scope.row.isActive)" effect="plain">{{ scope.row.isActive ? '启用' : '停用' }}</el-tag></template></el-table-column>
        <el-table-column label="操作" width="270" fixed="right"><template #default="scope">
          <div class="user-actions">
            <el-button size="small" plain :disabled="scope.row.id === session?.user.id" @click="openResetPassword(scope.row)"><el-icon><Key /></el-icon>重置密码</el-button>
            <el-button v-if="!isAdministrator(scope.row)" size="small" type="primary" plain :disabled="!scope.row.isActive" @click="toggleAdministrator(scope.row)">设为管理员</el-button>
            <el-button v-else size="small" type="warning" plain :disabled="!canRevokeAdministrator(scope.row)" @click="toggleAdministrator(scope.row)">{{ isSuperAdministrator(scope.row) ? '超级管理员' : scope.row.id === session?.user.id ? '当前管理员' : '取消管理员' }}</el-button>
            <el-button text circle :disabled="scope.row.id === session?.user.id && scope.row.isActive" :type="scope.row.isActive ? 'danger' : 'success'" :icon="SwitchButton" :aria-label="scope.row.isActive ? '停用用户' : '启用用户'" @click="toggleActive(scope.row)" />
          </div>
        </template></el-table-column>
      </el-table>
    </div>

    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record">
        <div class="mobile-record__head"><div><strong>{{ item.displayName }}</strong><span>{{ item.phoneNumber }}</span></div><el-tag :type="statusType(item.isActive)" effect="plain">{{ item.isActive ? '启用' : '停用' }}</el-tag></div>
        <div class="user-role-tags mobile-user-roles"><el-tag v-for="role in item.roles" :key="role" effect="plain">{{ roleLabel(role) }}</el-tag><el-tag v-if="isSuperAdministrator(item)" type="warning" effect="plain">超级管理员</el-tag></div>
        <div class="mobile-record__actions">
          <el-button size="small" plain :disabled="item.id === session?.user.id" @click="openResetPassword(item)"><el-icon><Key /></el-icon>重置密码</el-button>
          <el-button v-if="!isAdministrator(item)" size="small" type="primary" plain :disabled="!item.isActive" @click="toggleAdministrator(item)">设为管理员</el-button>
          <el-button v-else size="small" type="warning" plain :disabled="!canRevokeAdministrator(item)" @click="toggleAdministrator(item)">{{ isSuperAdministrator(item) ? '超级管理员' : item.id === session?.user.id ? '当前管理员' : '取消管理员' }}</el-button>
          <el-button size="small" :disabled="item.id === session?.user.id && item.isActive" :type="item.isActive ? 'danger' : 'success'" plain :icon="SwitchButton" @click="toggleActive(item)">{{ item.isActive ? '停用账户' : '启用账户' }}</el-button>
        </div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="当前没有正式用户" />
    </div>

    <el-pagination v-if="total > pageSize" class="pagination" v-model:current-page="filters.page" :page-size="pageSize" :total="total" layout="prev, pager, next" @current-change="load" />

    <el-dialog v-model="resetDialog.visible" title="重置用户密码" width="420px" :close-on-click-modal="false">
      <p v-if="resetDialog.target" class="dialog-note">正在为“{{ resetDialog.target.displayName }}”设置新密码。请通过安全渠道通知目标用户。</p>
      <el-form label-position="top" @submit.prevent="resetPassword">
        <el-form-item label="新密码"><el-input v-model="resetDialog.newPassword" type="password" show-password autocomplete="new-password" placeholder="至少 8 位且包含数字" /></el-form-item>
        <el-form-item label="确认新密码"><el-input v-model="resetDialog.confirmPassword" type="password" show-password autocomplete="new-password" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="resetDialog.visible = false">取消</el-button><el-button type="primary" :loading="resetDialog.loading" @click="resetPassword">确认重置</el-button></template>
    </el-dialog>
  </section>
</template>
