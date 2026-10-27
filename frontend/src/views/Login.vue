<template>
  <div class="login-wrap">
    <el-card class="login-card">
      <h2 class="title">电商销售管理系统</h2>
      <p class="sub">销售 / 补单 / 推广费一体化管理</p>
      <el-form :model="form" size="large" @keyup.enter="onLogin">
        <el-form-item>
          <el-input v-model="form.username" placeholder="用户名" :prefix-icon="User" autocomplete="username" />
        </el-form-item>
        <el-form-item>
          <el-input
            v-model="form.password"
            type="password"
            placeholder="密码"
            :prefix-icon="Lock"
            show-password
            autocomplete="current-password"
          />
        </el-form-item>
        <el-button type="primary" style="width: 100%" :loading="loading" @click="onLogin">登 录</el-button>
      </el-form>
      <p class="tip">默认账号：admin / Admin@123</p>
    </el-card>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { User, Lock } from '@element-plus/icons-vue'
import { login } from '../api'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)
const form = reactive({ username: '', password: '' })

async function onLogin() {
  if (!form.username || !form.password) return ElMessage.warning('请输入用户名和密码')
  loading.value = true
  try {
    const data = await login(form)
    auth.setAuth(data.token, data.user)
    router.push('/dashboard')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-wrap {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1d2935 0%, #2c3e50 60%, #3a5169 100%);
}
.login-card {
  width: 380px;
  padding: 12px 8px;
}
.title {
  text-align: center;
  margin: 4px 0 2px;
  color: #303133;
}
.sub {
  text-align: center;
  color: #909399;
  font-size: 13px;
  margin: 0 0 22px;
}
.tip {
  text-align: center;
  color: #c0c4cc;
  font-size: 12px;
  margin-top: 14px;
}
</style>
