<template>
  <el-container class="layout">
    <el-aside width="210px" class="aside">
      <div class="logo">
        <el-icon :size="20"><DataLine /></el-icon>
        <span>电商销售管理系统</span>
      </div>
      <el-menu
        :default-active="$route.path"
        router
        background-color="#1d2935"
        text-color="#a7b1c2"
        active-text-color="#ffffff"
      >
        <el-menu-item index="/dashboard">
          <el-icon><Odometer /></el-icon><span>数据看板</span>
        </el-menu-item>

        <el-sub-menu index="entry">
          <template #title><el-icon><EditPen /></el-icon><span>数据录入</span></template>
          <el-menu-item index="/sale-entry">销售录入</el-menu-item>
          <el-menu-item index="/supplement-entry">补单录入</el-menu-item>
          <el-menu-item index="/promotion-entry">推广费录入</el-menu-item>
        </el-sub-menu>

        <el-menu-item index="/data-list">
          <el-icon><Document /></el-icon><span>数据明细</span>
        </el-menu-item>

        <el-menu-item index="/report">
          <el-icon><TrendCharts /></el-icon><span>统计报表</span>
        </el-menu-item>

        <el-sub-menu index="base">
          <template #title><el-icon><Files /></el-icon><span>基础数据</span></template>
          <el-menu-item index="/shops">店铺管理</el-menu-item>
          <el-menu-item index="/masters">负责人 / 主体 / 字典</el-menu-item>
        </el-sub-menu>

        <el-menu-item v-if="auth.isAdmin" index="/users">
          <el-icon><UserFilled /></el-icon><span>用户管理</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="header">
        <div class="header-title">{{ $route.meta?.title || '' }}</div>
        <el-dropdown @command="onCommand">
          <span class="user-chip">
            <el-icon><User /></el-icon>
            {{ auth.user?.realName || auth.user?.username }}
            <el-tag size="small" style="margin-left: 6px">{{ roleText }}</el-tag>
          </span>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="logout">退出登录</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>
      <el-main class="main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()

const roleText = computed(() => ({ admin: '管理员', operator: '录入员', viewer: '只读' }[auth.user?.role] || ''))

function onCommand(cmd) {
  if (cmd === 'logout') {
    auth.logout()
    router.push('/login')
  }
}
</script>

<style scoped>
.layout {
  height: 100%;
}
.aside {
  background: #1d2935;
  overflow-x: hidden;
}
.logo {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: #fff;
  font-weight: 600;
  font-size: 15px;
  letter-spacing: 1px;
}
.aside :deep(.el-menu) {
  border-right: none;
}
.header {
  background: #fff;
  display: flex;
  align-items: center;
  justify-content: space-between;
  box-shadow: 0 1px 4px rgba(0, 21, 41, 0.08);
  z-index: 5;
}
.header-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}
.user-chip {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #303133;
  font-size: 14px;
}
.main {
  padding: 16px;
  overflow: auto;
}
</style>
