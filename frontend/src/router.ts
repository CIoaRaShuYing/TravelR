import { createRouter, createWebHistory } from 'vue-router'
import MyClaimsView from './views/MyClaimsView.vue'
import AdminRegistrationsView from './views/AdminRegistrationsView.vue'
import AdminProjectsView from './views/AdminProjectsView.vue'
import AdminSettingsView from './views/AdminSettingsView.vue'
import AdminClaimsView from './views/AdminClaimsView.vue'
import { isAdministrator, session } from './session'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/claims' },
    { path: '/claims', component: MyClaimsView, meta: { title: '我的报销' } },
    { path: '/admin/registrations', component: AdminRegistrationsView, meta: { title: '注册审批', administrator: true } },
    { path: '/admin/projects', component: AdminProjectsView, meta: { title: '项目管理', administrator: true } },
    { path: '/admin/claims', component: AdminClaimsView, meta: { title: '报销管理', administrator: true } },
    { path: '/admin/settings', component: AdminSettingsView, meta: { title: '注册策略', administrator: true } },
    { path: '/:pathMatch(.*)*', redirect: '/claims' },
  ],
})

router.beforeEach((to) => {
  if (session.value && to.meta.administrator && !isAdministrator.value) return '/claims'
})
