<template>
  <div class="page-card">
    <el-tabs v-model="tab" @tab-change="loadCurrent">
      <el-tab-pane label="负责人" name="owner" />
      <el-tab-pane label="公司主体" name="company" />
      <el-tab-pane label="下单人" name="orderer" />
      <el-tab-pane label="字典管理" name="dict" />
    </el-tabs>

    <!-- 负责人 / 公司主体 / 下单人 -->
    <template v-if="tab !== 'dict'">
      <div class="filter-bar">
        <div style="flex: 1"></div>
        <el-button type="primary" plain :disabled="!auth.isAdmin" @click="openAdd">新增{{ kindName }}</el-button>
      </div>
      <el-table v-loading="loading" :data="masterItems" border stripe size="small">
        <el-table-column type="index" label="#" width="50" />
        <el-table-column label="名称" min-width="220">
          <template #default="{ row }">
            {{ row.name }}
            <el-tag v-if="row.status === 0" size="small" type="info" style="margin-left: 6px">停用</el-tag>
          </template>
        </el-table-column>
        <el-table-column v-if="tab === 'company'" label="信用代码" min-width="200" prop="creditCode" />
        <el-table-column label="备注" min-width="160" prop="remark" />
        <el-table-column label="操作" width="180">
          <template #default="{ row }">
            <el-button link type="primary" :disabled="!auth.isAdmin" @click="openEdit(row)">编辑</el-button>
            <el-button link :type="row.status === 1 ? 'warning' : 'success'" :disabled="!auth.isAdmin" @click="toggle(row)">
              {{ row.status === 1 ? '停用' : '启用' }}
            </el-button>
            <el-button link type="danger" :disabled="!auth.isAdmin" @click="remove(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </template>

    <!-- 字典管理 -->
    <template v-else>
      <div class="filter-bar">
        <el-radio-group v-model="dictType" @change="loadDict">
          <el-radio-button value="platform">平台</el-radio-button>
          <el-radio-button value="supplement_reason">补单原因</el-radio-button>
          <el-radio-button value="promo_type">推广类型</el-radio-button>
        </el-radio-group>
        <div style="flex: 1"></div>
        <el-button type="primary" plain :disabled="!auth.canEdit" @click="openDictAdd">新增字典项</el-button>
      </div>
      <el-table v-loading="loading" :data="dictItems" border stripe size="small">
        <el-table-column label="编码" width="140" prop="itemCode" />
        <el-table-column label="名称" min-width="180" prop="itemName">
          <template #default="{ row }">
            {{ row.itemName }}
            <el-tag v-if="row.status === 0" size="small" type="info" style="margin-left: 6px">停用</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="排序" width="90" prop="sortNo" />
        <el-table-column label="操作" width="180">
          <template #default="{ row }">
            <el-button link type="primary" :disabled="!auth.canEdit" @click="openDictEdit(row)">编辑</el-button>
            <el-button link :type="row.status === 1 ? 'warning' : 'success'" :disabled="!auth.isAdmin" @click="toggleDict(row)">
              {{ row.status === 1 ? '停用' : '启用' }}
            </el-button>
            <el-button link type="danger" :disabled="!auth.isAdmin" @click="removeDict(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </template>

    <!-- 资料编辑 -->
    <el-dialog v-model="masterDialog" :title="`${editingId ? '编辑' : '新增'}${kindName}`" width="440px">
      <el-form label-width="90px">
        <el-form-item label="名称" required>
          <el-input v-model="masterForm.name" />
        </el-form-item>
        <el-form-item v-if="tab === 'company'" label="信用代码">
          <el-input v-model="masterForm.creditCode" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="masterForm.remark" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="masterDialog = false">取 消</el-button>
        <el-button type="primary" @click="saveMaster">保 存</el-button>
      </template>
    </el-dialog>

    <!-- 字典编辑 -->
    <el-dialog v-model="dictDialog" :title="`${dictEditingId ? '编辑' : '新增'}字典项`" width="440px">
      <el-form label-width="90px">
        <el-form-item v-if="!dictEditingId" label="编码" required>
          <el-input v-model="dictForm.itemCode" placeholder="如 tb / ztc" :disabled="!!dictEditingId" />
        </el-form-item>
        <el-form-item label="名称" required>
          <el-input v-model="dictForm.itemName" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="dictForm.sortNo" :min="0" :controls="false" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dictDialog = false">取 消</el-button>
        <el-button type="primary" @click="saveDict">保 存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { createDict, createMaster, deleteDict, deleteMaster, getDicts, getMasters, updateDict, updateMaster } from '../api'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const tab = ref('owner')
const loading = ref(false)
const masterItems = ref([])
const dictItems = ref([])
const dictType = ref('platform')

const masterDialog = ref(false)
const editingId = ref(null)
const masterForm = ref({})
const dictDialog = ref(false)
const dictEditingId = ref(null)
const dictForm = ref({})

const kindName = computed(() => ({ owner: '负责人', company: '公司主体', orderer: '下单人' }[tab.value] || ''))

async function loadCurrent() {
  loading.value = true
  try {
    if (tab.value === 'dict') {
      dictItems.value = await getDicts(dictType.value)
    } else {
      masterItems.value = await getMasters(tab.value)
    }
  } finally {
    loading.value = false
  }
}
const loadDict = loadCurrent

function openAdd() {
  editingId.value = null
  masterForm.value = { name: '', creditCode: '', remark: '' }
  masterDialog.value = true
}
function openEdit(row) {
  editingId.value = row.id
  masterForm.value = { name: row.name, creditCode: row.creditCode || '', remark: row.remark || '' }
  masterDialog.value = true
}
async function saveMaster() {
  if (!masterForm.value.name) return ElMessage.warning('名称不能为空')
  if (editingId.value) {
    await updateMaster(tab.value, editingId.value, { ...masterForm.value, status: 1 })
  } else {
    await createMaster(tab.value, masterForm.value)
  }
  ElMessage.success('已保存')
  masterDialog.value = false
  loadCurrent()
}
async function toggle(row) {
  await updateMaster(tab.value, row.id, {
    name: row.name,
    creditCode: row.creditCode,
    remark: row.remark,
    status: row.status === 1 ? 0 : 1
  })
  ElMessage.success('状态已更新')
  loadCurrent()
}
async function remove(row) {
  await ElMessageBox.confirm(`确认删除「${row.name}」？已被业务数据引用时将无法删除（可改为停用）`, '删除确认', { type: 'warning' })
  try {
    await deleteMaster(tab.value, row.id)
    ElMessage.success('已删除')
  } finally {
    loadCurrent()
  }
}

function openDictAdd() {
  dictEditingId.value = null
  dictForm.value = { itemCode: '', itemName: '', sortNo: 0 }
  dictDialog.value = true
}
function openDictEdit(row) {
  dictEditingId.value = row.id
  dictForm.value = { itemCode: row.itemCode, itemName: row.itemName, sortNo: row.sortNo }
  dictDialog.value = true
}
async function saveDict() {
  if (!dictForm.value.itemName) return ElMessage.warning('名称不能为空')
  if (dictEditingId.value) {
    await updateDict(dictEditingId.value, { itemName: dictForm.value.itemName, sortNo: dictForm.value.sortNo, status: 1 })
  } else {
    if (!dictForm.value.itemCode) return ElMessage.warning('编码不能为空')
    await createDict({ dictType: dictType.value, ...dictForm.value })
  }
  ElMessage.success('已保存')
  dictDialog.value = false
  loadCurrent()
}
async function toggleDict(row) {
  await updateDict(row.id, { itemName: row.itemName, sortNo: row.sortNo, status: row.status === 1 ? 0 : 1 })
  ElMessage.success('状态已更新')
  loadCurrent()
}
async function removeDict(row) {
  await ElMessageBox.confirm(`确认删除字典项「${row.itemName}」？`, '删除确认', { type: 'warning' })
  await deleteDict(row.id)
  ElMessage.success('已删除')
  loadCurrent()
}

onMounted(loadCurrent)
</script>
