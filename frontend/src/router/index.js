import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  { path: '/login', name: 'login', component: () => import('../views/Login.vue'), meta: { title: '登录' } },
  {
    path: '/',
    component: () => import('../layout/MainLayout.vue'),
    redirect: '/dashboard',
    children: [
      { path: 'dashboard', name: 'dashboard', component: () => import('../views/Dashboard.vue'), meta: { title: '数据看板' } },
      { path: 'sale-entry', name: 'saleEntry', component: () => import('../views/SaleEntry.vue'), meta: { title: '销售录入' } },
      { path: 'supplement-entry', name: 'supplementEntry', component: () => import('../views/SupplementEntry.vue'), meta: { title: '补单录入' } },
      { path: 'promotion-entry', name: 'promotionEntry', component: () => import('../views/PromotionEntry.vue'), meta: { title: '推广费录入' } },
      { path: 'data-list', name: 'dataList', component: () => import('../views/DataList.vue'), meta: { title: '数据明细' } },
      { path: 'report', name: 'report', component: () => import('../views/ReportView.vue'), meta: { title: '统计报表' } },
      { path: 'shops', name: 'shops', component: () => import('../views/ShopManage.vue'), meta: { title: '店铺管理' } },
      { path: 'masters', name: 'masters', component: () => import('../views/MasterData.vue'), meta: { title: '基础数据' } },
      { path: 'users', name: 'users', component: () => import('../views/UserManage.vue'), meta: { title: '用户管理', roles: ['admin'] } }
    ]
  },
  { path: '/:pathMatch(.*)*', redirect: '/dashboard' }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.path !== '/login' && !auth.token) return '/login'
  if (to.meta?.roles && !to.meta.roles.includes(auth.user?.role)) return '/dashboard'
  document.title = to.meta?.title ? `${to.meta.title} - 电商销售管理系统` : '电商销售管理系统'
  return true
})

export default router
