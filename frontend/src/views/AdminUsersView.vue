<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, SwitchButton } from '@element-plus/icons-vue'
import { api, type AdminUser } from '../api'
import { session } from '../session'

type ActiveFilter = 'all' | 'active' | 'inactive'

const rows = ref<AdminUser[]>([])
const loading = ref(false)
const total = ref(0)
const pageSize = 20
const filters = reactive<{ keyword: string; active: ActiveFilter; page: number }>({ keyword: '', active: 'all', page: 1 })

const roleLabels: Record<string, string> = { Applicant: '申请人', Administrator: '管理员' }

function roleLabel(role: string) { return roleLabels[role] ?? role }
function statusType(active: boolean) { return active ? 'success' : 'info' }

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

onMounted(load)
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">USER DIRECTORY</p><h1>用户中心</h1><p>管理正式用户账户状态；停用不会删除历史报销或审计记录。</p></div>
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
        <el-table-column label="角色" min-width="170"><template #default="scope"><div class="user-role-tags"><el-tag v-for="role in scope.row.roles" :key="role" effect="plain">{{ roleLabel(role) }}</el-tag></div></template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="scope"><el-tag :type="statusType(scope.row.isActive)" effect="plain">{{ scope.row.isActive ? '启用' : '停用' }}</el-tag></template></el-table-column>
        <el-table-column label="操作" width="120" fixed="right"><template #default="scope"><el-tooltip :content="scope.row.id === session?.user.id ? '当前登录账户不能停用' : scope.row.isActive ? '停用用户' : '启用用户'"><el-button text circle :disabled="scope.row.id === session?.user.id && scope.row.isActive" :type="scope.row.isActive ? 'danger' : 'success'" :icon="SwitchButton" :aria-label="scope.row.isActive ? '停用用户' : '启用用户'" @click="toggleActive(scope.row)" /></el-tooltip></template></el-table-column>
      </el-table>
    </div>

    <div class="mobile-list" v-loading="loading">
      <article v-for="item in rows" :key="item.id" class="mobile-record">
        <div class="mobile-record__head"><div><strong>{{ item.displayName }}</strong><span>{{ item.phoneNumber }}</span></div><el-tag :type="statusType(item.isActive)" effect="plain">{{ item.isActive ? '启用' : '停用' }}</el-tag></div>
        <div class="user-role-tags mobile-user-roles"><el-tag v-for="role in item.roles" :key="role" effect="plain">{{ roleLabel(role) }}</el-tag></div>
        <div class="mobile-record__actions"><el-button :disabled="item.id === session?.user.id && item.isActive" :type="item.isActive ? 'danger' : 'success'" plain :icon="SwitchButton" @click="toggleActive(item)">{{ item.id === session?.user.id ? '当前账户' : item.isActive ? '停用账户' : '启用账户' }}</el-button></div>
      </article>
      <el-empty v-if="!loading && rows.length === 0" description="当前没有正式用户" />
    </div>

    <el-pagination v-if="total > pageSize" class="pagination" v-model:current-page="filters.page" :page-size="pageSize" :total="total" layout="prev, pager, next" @current-change="load" />
  </section>
</template>
