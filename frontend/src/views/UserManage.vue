<template>
  <div class="page-card">
    <div class="filter-bar">
      <div style="flex: 1"></div>
      <el-button type="primary" plain @click="openAdd">新增用户</el-button>
    </div>

    <el-table v-loading="loading" :data="items" border stripe size="small">
      <el-table-column type="index" label="#" width="50" />
      <el-table-column label="登录名" width="140" prop="username" />
      <el-table-column label="姓名" width="140" prop="realName" />
      <el-table-column label="角色" width="120">
        <template #default="{ row }">
          <el-tag :type="{ admin: 'danger', operator: 'primary', viewer: 'info' }[row.role]" size="small">{{ roleText(row.role) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'info'" size="small">{{ row.status === 1 ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="170" prop="createdAt" />
      <el-table-column label="操作" width="220">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="warning" @click="openResetPwd(row)">重置密码</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialog" :title="editingId ? '编辑用户' : '新增用户'" width="440px">
      <el-form label-width="90px">
        <el-form-item label="登录名" required>
          <el-input v-model="form.username" :disabled="!!editingId" />
        </el-form-item>
        <el-form-item label="姓名">
          <el-input v-model="form.realName" />
        </el-form-item>
        <el-form-item label="角色" required>
          <el-select v-model="form.role" style="width: 100%">
            <el-option label="管理员" value="admin" />
            <el-option label="录入员" value="operator" />
            <el-option label="只读" value="viewer" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="!editingId" label="初始密码" required>
          <el-input v-model="form.password" show-password placeholder="至少 6 位" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialog = false">取 消</el-button>
        <el-button type="primary" @click="save">保 存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="pwdDialog" title="重置密码" width="380px">
      <el-form label-width="90px">
        <el-form-item label="新密码" required>
          <el-input v-model="newPwd" show-password placeholder="至少 6 位" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdDialog = false">取 消</el-button>
        <el-button type="primary" @click="savePwd">确 定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { createUser, deleteUser, getUsers, resetPassword, updateUser } from '../api'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const items = ref([])
const loading = ref(false)
const dialog = ref(false)
const editingId = ref(null)
const form = ref({})
const pwdDialog = ref(false)
const pwdUserId = ref(null)
const newPwd = ref('')

const roleText = (r) => ({ admin: '管理员', operator: '录入员', viewer: '只读' }[r] || r)

async function load() {
  loading.value = true
  try {
    items.value = await getUsers()
  } finally {
    loading.value = false
  }
}

function openAdd() {
  editingId.value = null
  form.value = { username: '', realName: '', role: 'operator', password: '' }
  dialog.value = true
}
function openEdit(row) {
  editingId.value = row.id
  form.value = { username: row.username, realName: row.realName, role: row.role }
  dialog.value = true
}
async function save() {
  if (editingId.value) {
    await updateUser(editingId.value, { realName: form.value.realName, role: form.value.role, status: 1 })
    ElMessage.success('已保存')
  } else {
    await createUser(form.value)
    ElMessage.success('用户已创建')
  }
  dialog.value = false
  load()
}
function openResetPwd(row) {
  pwdUserId.value = row.id
  newPwd.value = ''
  pwdDialog.value = true
}
async function savePwd() {
  if (!newPwd.value || newPwd.value.length < 6) return ElMessage.warning('密码至少 6 位')
  await resetPassword(pwdUserId.value, { password: newPwd.value })
  ElMessage.success('密码已重置')
  pwdDialog.value = false
}
async function remove(row) {
  if (row.id === auth.user?.id) return ElMessage.warning('不能删除当前登录账号')
  await ElMessageBox.confirm(`确认删除用户「${row.username}」？`, '删除确认', { type: 'warning' })
  await deleteUser(row.id)
  ElMessage.success('已删除')
  load()
}

onMounted(load)
</script>
