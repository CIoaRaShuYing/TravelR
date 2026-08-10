import { createRouter, createWebHistory } from 'vue-router'
import MyClaimsView from './views/MyClaimsView.vue'
import AccountSecurityView from './views/AccountSecurityView.vue'
import AdminRegistrationsView from './views/AdminRegistrationsView.vue'
import AdminUsersView from './views/AdminUsersView.vue'
import AdminProjectsView from './views/AdminProjectsView.vue'
import AdminSettingsView from './views/AdminSettingsView.vue'
import AdminClaimsView from './views/AdminClaimsView.vue'
import ProfileView from './views/ProfileView.vue'
import WeeklyReportsView from './views/WeeklyReportsView.vue'
import { isAdministrator, profileIncomplete, session } from './session'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/claims' },
    { path: '/claims', component: MyClaimsView, meta: { title: '我的报销' } },
    { path: '/account/profile', component: ProfileView, meta: { title: '个人资料' } },
    { path: '/account/security', component: AccountSecurityView, meta: { title: '账号安全' } },
    { path: '/weekly-reports', component: WeeklyReportsView, meta: { title: '项目周报' } },
    { path: '/admin/users', component: AdminUsersView, meta: { title: '用户中心', administrator: true } },
    { path: '/admin/registrations', component: AdminRegistrationsView, meta: { title: '注册审批', administrator: true } },
    { path: '/admin/projects', component: AdminProjectsView, meta: { title: '项目管理', administrator: true } },
    { path: '/admin/claims', component: AdminClaimsView, meta: { title: '报销管理', administrator: true } },
    { path: '/admin/settings', component: AdminSettingsView, meta: { title: '注册策略', administrator: true } },
    { path: '/:pathMatch(.*)*', redirect: '/claims' },
  ],
})

router.beforeEach((to) => {
  if (session.value && profileIncomplete.value && !['/account/profile', '/account/security'].includes(to.path)) return '/account/profile'
  if (session.value && to.meta.administrator && !isAdministrator.value) return '/claims'
})
