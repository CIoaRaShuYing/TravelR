<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Calendar, CreditCard, DocumentChecked, FolderOpened, Key, Menu, Setting, SwitchButton, Tickets, User, UserFilled } from '@element-plus/icons-vue'
import { clearSession, isAdministrator, profileIncomplete, session } from '../session'

const route = useRoute()
const router = useRouter()
const drawerOpen = ref(false)

const navItems = computed(() => [
  { path: '/account/profile', label: '个人资料', icon: CreditCard, visible: true },
  { path: '/claims', label: '我的报销', icon: Tickets, visible: !profileIncomplete.value },
  { path: '/weekly-reports', label: '项目周报', icon: Calendar, visible: !profileIncomplete.value },
  { path: '/account/security', label: '账号安全', icon: Key, visible: true },
  { path: '/admin/users', label: '用户中心', icon: UserFilled, visible: isAdministrator.value && !profileIncomplete.value },
  { path: '/admin/registrations', label: '注册审批', icon: User, visible: isAdministrator.value && !profileIncomplete.value },
  { path: '/admin/projects', label: '项目管理', icon: FolderOpened, visible: isAdministrator.value && !profileIncomplete.value },
  { path: '/admin/claims', label: '报销管理', icon: DocumentChecked, visible: isAdministrator.value && !profileIncomplete.value },
  { path: '/admin/settings', label: '注册策略', icon: Setting, visible: isAdministrator.value && !profileIncomplete.value },
].filter(item => item.visible))

async function navigate(path: string) {
  drawerOpen.value = false
  await router.push(path)
}

async function signOut() {
  clearSession()
  await router.replace('/claims')
}
</script>

<template>
  <section class="workspace">
    <aside class="sidebar">
      <div class="brand brand--light"><span class="brand-mark">行</span><span>差旅账</span></div>
      <p class="nav-label">工作台</p>
      <nav class="nav-list" aria-label="主导航">
        <button v-for="item in navItems" :key="item.path" :class="{ active: route.path.startsWith(item.path) }" type="button" @click="navigate(item.path)">
          <el-icon><component :is="item.icon" /></el-icon><span>{{ item.label }}</span>
        </button>
      </nav>
      <div class="account">
        <span class="account-avatar">{{ session?.user.displayName.slice(0, 1) }}</span>
        <div><strong>{{ session?.user.displayName }}</strong><span>{{ isAdministrator ? '管理员' : '申请人' }}</span></div>
        <el-tooltip content="退出登录" placement="top"><button class="icon-action" type="button" aria-label="退出登录" @click="signOut"><el-icon><SwitchButton /></el-icon></button></el-tooltip>
      </div>
    </aside>

    <header class="mobile-header">
      <button class="icon-action icon-action--light" type="button" aria-label="打开导航" @click="drawerOpen = true"><el-icon><Menu /></el-icon></button>
      <div class="brand brand--light"><span class="brand-mark">行</span><span>差旅账</span></div>
      <span class="mobile-avatar">{{ session?.user.displayName.slice(0, 1) }}</span>
    </header>

    <el-drawer v-model="drawerOpen" direction="ltr" size="278px" :with-header="false" class="mobile-drawer">
      <div class="drawer-content">
        <div class="brand"><span class="brand-mark">行</span><span>差旅账</span></div>
        <nav class="nav-list nav-list--drawer">
          <button v-for="item in navItems" :key="item.path" :class="{ active: route.path.startsWith(item.path) }" type="button" @click="navigate(item.path)">
            <el-icon><component :is="item.icon" /></el-icon><span>{{ item.label }}</span>
          </button>
        </nav>
        <button class="drawer-signout" type="button" @click="signOut"><el-icon><SwitchButton /></el-icon>退出登录</button>
      </div>
    </el-drawer>

    <main class="content"><router-view /></main>
  </section>
</template>
