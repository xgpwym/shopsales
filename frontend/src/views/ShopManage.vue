<template>
  <div class="page-card">
    <div class="filter-bar">
      <el-select v-model="filters.platform" placeholder="平台" clearable filterable style="width: 150px">
        <el-option v-for="p in platforms" :key="p.itemCode" :label="p.itemName" :value="p.itemCode" />
      </el-select>
      <el-select v-model="filters.status" placeholder="状态" clearable style="width: 110px">
        <el-option label="启用" :value="1" />
        <el-option label="停用" :value="0" />
      </el-select>
      <el-select v-model="filters.ownerId" placeholder="负责人" clearable filterable style="width: 150px">
        <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
      </el-select>
      <el-input v-model="filters.keyword" placeholder="店铺名称关键字" clearable style="width: 180px" @keyup.enter="load" />
      <el-button type="primary" @click="load">查 询</el-button>
      <div style="flex: 1"></div>
      <el-button type="primary" plain :disabled="!auth.canEdit" @click="openAdd">新增店铺</el-button>
    </div>

    <el-table v-loading="loading" :data="items" border stripe size="small">
      <el-table-column label="平台" width="110">
        <template #default="{ row }">
          <el-tag size="small">{{ row.platformName }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="店铺名称" min-width="150" prop="name" />
      <el-table-column label="负责人（当前）" width="120" prop="ownerName" />
      <el-table-column label="公司主体（当前）" min-width="200" prop="companyName" show-overflow-tooltip />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-switch :model-value="row.status === 1" :disabled="!auth.isAdmin" @change="(v) => onToggle(row, v)" />
        </template>
      </el-table-column>
      <el-table-column label="备注" min-width="120" prop="remark" show-overflow-tooltip />
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" :disabled="!auth.canEdit" @click="openEdit(row)">编辑</el-button>
          <el-button link type="primary" :disabled="!auth.canEdit" @click="openOwnerChange(row)">换负责人</el-button>
          <el-button link type="primary" :disabled="!auth.canEdit" @click="openCompanyChange(row)">换主体</el-button>
          <el-button link type="info" @click="openHistory(row)">变更历史</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 新增/编辑店铺 -->
    <el-dialog v-model="shopDialog" :title="editingId ? '编辑店铺' : '新增店铺'" width="480px">
      <el-form label-width="100px">
        <el-form-item label="店铺名称" required>
          <el-input v-model="shopForm.name" placeholder="如：礼遇甘肃" />
        </el-form-item>
        <el-form-item label="平台" required>
          <el-select v-model="shopForm.platformCode" style="width: 100%">
            <el-option v-for="p in platforms" :key="p.itemCode" :label="p.itemName" :value="p.itemCode" />
          </el-select>
        </el-form-item>
        <template v-if="!editingId">
          <el-form-item label="初始负责人" required>
            <el-select v-model="shopForm.ownerId" filterable style="width: 100%">
              <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
            </el-select>
          </el-form-item>
          <el-form-item label="初始公司主体" required>
            <el-select v-model="shopForm.companyId" filterable style="width: 100%">
              <el-option v-for="c in companies" :key="c.id" :label="c.name" :value="c.id" />
            </el-select>
          </el-form-item>
          <div class="muted" style="margin: -8px 0 12px 100px">初始归属以今日生效写入版本表，后续变更请使用「换负责人 / 换主体」</div>
        </template>
        <el-form-item label="备注">
          <el-input v-model="shopForm.remark" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="shopDialog = false">取 消</el-button>
        <el-button type="primary" @click="saveShop">保 存</el-button>
      </template>
    </el-dialog>

    <!-- 换负责人 -->
    <el-dialog v-model="ownerDialog" title="变更负责人（版本化）" width="440px">
      <el-alert type="warning" :closable="false" style="margin-bottom: 14px"
        title="自生效日期起新负责人接管该店铺；生效日期之前的历史数据归属保持不变" />
      <el-form label-width="100px">
        <el-form-item label="店铺"><span>{{ currentShop?.name }}</span></el-form-item>
        <el-form-item label="新负责人" required>
          <el-select v-model="ownerForm.ownerId" filterable style="width: 100%">
            <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="生效日期" required>
          <el-date-picker v-model="ownerForm.effectiveDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" :clearable="false" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="ownerDialog = false">取 消</el-button>
        <el-button type="primary" @click="saveOwnerChange">保存变更</el-button>
      </template>
    </el-dialog>

    <!-- 换主体 -->
    <el-dialog v-model="companyDialog" title="变更公司主体（版本化）" width="440px">
      <el-alert type="warning" :closable="false" style="margin-bottom: 14px"
        title="自生效日期起新主体接管该店铺；生效日期之前的历史数据主体保持不变" />
      <el-form label-width="100px">
        <el-form-item label="店铺"><span>{{ currentShop?.name }}</span></el-form-item>
        <el-form-item label="新公司主体" required>
          <el-select v-model="companyForm.companyId" filterable style="width: 100%">
            <el-option v-for="c in companies" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="生效日期" required>
          <el-date-picker v-model="companyForm.effectiveDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" :clearable="false" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="companyDialog = false">取 消</el-button>
        <el-button type="primary" @click="saveCompanyChange">保存变更</el-button>
      </template>
    </el-dialog>

    <!-- 变更历史 -->
    <el-dialog v-model="historyDialog" :title="`变更历史 - ${currentShop?.name || ''}`" width="560px">
      <h4 class="panel-title">负责人版本</h4>
      <el-timeline style="padding-left: 10px">
        <el-timeline-item v-for="v in ownerVersions" :key="'o' + v.id" :timestamp="`自 ${v.effectiveDate?.slice(0, 10)} 起`" placement="top">
          {{ v.ownerName }}
        </el-timeline-item>
      </el-timeline>
      <el-divider />
      <h4 class="panel-title">公司主体版本</h4>
      <el-timeline style="padding-left: 10px">
        <el-timeline-item v-for="v in companyVersions" :key="'c' + v.id" :timestamp="`自 ${v.effectiveDate?.slice(0, 10)} 起`" placement="top">
          {{ v.companyName }}
        </el-timeline-item>
      </el-timeline>
    </el-dialog>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import dayjs from 'dayjs'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  addCompanyVersion, addOwnerVersion, createShop, getCompanyVersions, getDicts, getMasters,
  getOwnerVersions, getShops, setShopStatus, updateShop
} from '../api'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const platforms = ref([])
const owners = ref([])
const companies = ref([])
const filters = ref({})
const items = ref([])
const loading = ref(false)

const shopDialog = ref(false)
const editingId = ref(null)
const shopForm = ref({})
const ownerDialog = ref(false)
const companyDialog = ref(false)
const ownerForm = ref({})
const companyForm = ref({})
const currentShop = ref(null)
const historyDialog = ref(false)
const ownerVersions = ref([])
const companyVersions = ref([])

async function load() {
  loading.value = true
  try {
    items.value = await getShops({ ...filters.value, status: filters.value.status ?? undefined })
  } finally {
    loading.value = false
  }
}

async function openAdd() {
  editingId.value = null
  shopForm.value = { name: '', platformCode: null, ownerId: null, companyId: null, remark: '' }
  shopDialog.value = true
}

function openEdit(row) {
  editingId.value = row.id
  shopForm.value = { name: row.name, platformCode: row.platformCode, remark: row.remark || '' }
  shopDialog.value = true
}

async function saveShop() {
  const f = shopForm.value
  if (!f.name || !f.platformCode) return ElMessage.warning('请填写店铺名称和平台')
  if (editingId.value) {
    await updateShop(editingId.value, f)
    ElMessage.success('已保存')
  } else {
    if (!f.ownerId || !f.companyId) return ElMessage.warning('请选择初始负责人和公司主体')
    await createShop(f)
    ElMessage.success('店铺已创建，初始归属已写入版本表')
  }
  shopDialog.value = false
  load()
}

async function onToggle(row, v) {
  await ElMessageBox.confirm(
    v ? `确认启用店铺「${row.name}」？启用后可恢复录入` : `确认停用店铺「${row.name}」？停用后不可再录入，历史数据保留`,
    '店铺状态变更',
    { type: 'warning' }
  )
  await setShopStatus(row.id, v ? 1 : 0)
  ElMessage.success('状态已更新')
  load()
}

function openOwnerChange(row) {
  currentShop.value = row
  ownerForm.value = { ownerId: null, effectiveDate: dayjs().format('YYYY-MM-DD') }
  ownerDialog.value = true
}
async function saveOwnerChange() {
  if (!ownerForm.value.ownerId) return ElMessage.warning('请选择新负责人')
  await addOwnerVersion(currentShop.value.id, ownerForm.value)
  ElMessage.success('负责人变更已生效')
  ownerDialog.value = false
  load()
}

function openCompanyChange(row) {
  currentShop.value = row
  companyForm.value = { companyId: null, effectiveDate: dayjs().format('YYYY-MM-DD') }
  companyDialog.value = true
}
async function saveCompanyChange() {
  if (!companyForm.value.companyId) return ElMessage.warning('请选择新公司主体')
  await addCompanyVersion(currentShop.value.id, companyForm.value)
  ElMessage.success('主体变更已生效')
  companyDialog.value = false
  load()
}

async function openHistory(row) {
  currentShop.value = row
  ;[ownerVersions.value, companyVersions.value] = await Promise.all([
    getOwnerVersions(row.id),
    getCompanyVersions(row.id)
  ])
  historyDialog.value = true
}

onMounted(async () => {
  ;[platforms.value, owners.value, companies.value] = await Promise.all([
    getDicts('platform'),
    getMasters('owner'),
    getMasters('company')
  ])
  await load()
})
</script>
