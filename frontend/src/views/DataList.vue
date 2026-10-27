<template>
  <div class="page-card">
    <el-tabs v-model="tab" @tab-change="loadPage(1)">
      <el-tab-pane label="销售明细" name="sale" />
      <el-tab-pane label="补单明细" name="supplement" />
      <el-tab-pane label="推广费明细" name="promotion" />
    </el-tabs>

    <div class="filter-bar">
      <el-date-picker v-model="range" type="daterange" value-format="YYYY-MM-DD" start-placeholder="开始日期" end-placeholder="结束日期" style="width: 260px" />
      <el-select v-model="filters.platform" placeholder="平台" clearable filterable>
        <el-option v-for="p in platforms" :key="p.itemCode" :label="p.itemName" :value="p.itemCode" />
      </el-select>
      <el-select v-model="filters.shopId" placeholder="店铺" clearable filterable>
        <el-option v-for="s in shops" :key="s.id" :label="s.name" :value="s.id" />
      </el-select>
      <el-select v-model="filters.ownerId" placeholder="负责人" clearable filterable>
        <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
      </el-select>
      <el-select v-model="filters.companyId" placeholder="公司主体" clearable filterable>
        <el-option v-for="c in companies" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <template v-if="tab === 'supplement'">
        <el-select v-model="filters.ordererId" placeholder="下单人" clearable filterable>
          <el-option v-for="o in orderers" :key="o.id" :label="o.name" :value="o.id" />
        </el-select>
        <el-select v-model="filters.refunded" placeholder="返款状态" clearable style="width: 130px">
          <el-option label="已返款" :value="1" />
          <el-option label="未返款" :value="0" />
        </el-select>
      </template>
      <template v-if="tab === 'promotion'">
        <el-select v-model="filters.promoType" placeholder="推广类型" clearable>
          <el-option v-for="t in promoTypes" :key="t.itemCode" :label="t.itemName" :value="t.itemCode" />
        </el-select>
      </template>
      <el-button type="primary" @click="loadPage(1)">查 询</el-button>
      <el-button @click="onReset">重 置</el-button>
      <div style="flex: 1"></div>
      <el-button :disabled="!auth.canEdit" @click="onImport">批量导入</el-button>
      <el-button type="success" plain @click="onExport">导出 Excel</el-button>
    </div>

    <!-- 销售表格 -->
    <el-table v-if="tab === 'sale'" v-loading="loading" :data="items" border stripe size="small">
      <el-table-column label="日期" width="105" prop="bizDate" />
      <el-table-column label="平台" width="100" prop="platformName" />
      <el-table-column label="店铺" min-width="140" prop="shopName" />
      <el-table-column label="负责人" width="90" prop="ownerName" />
      <el-table-column label="公司主体" min-width="170" prop="companyName" show-overflow-tooltip />
      <el-table-column label="销售额" width="110" align="right">
        <template #default="{ row }">{{ money(row.saleAmount) }}</template>
      </el-table-column>
      <el-table-column label="退款金额" width="110" align="right">
        <template #default="{ row }">{{ money(row.refundAmount) }}</template>
      </el-table-column>
      <el-table-column label="净销售额" width="110" align="right">
        <template #default="{ row }"><b>{{ money(row.netAmount) }}</b></template>
      </el-table-column>
      <el-table-column label="备注" min-width="120" prop="remark" show-overflow-tooltip />
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" :disabled="!auth.canEdit" @click="openSaleEdit(row)">编辑</el-button>
          <el-button link type="danger" :disabled="!auth.canEdit" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 补单表格 -->
    <el-table v-if="tab === 'supplement'" v-loading="loading" :data="items" border stripe size="small">
      <el-table-column label="日期" width="105" prop="bizDate" />
      <el-table-column label="平台" width="95" prop="platformName" />
      <el-table-column label="店铺" min-width="130" prop="shopName" />
      <el-table-column label="订单号" min-width="150" prop="orderNo" show-overflow-tooltip />
      <el-table-column label="下单人" width="85" prop="ordererName" />
      <el-table-column label="金额" width="100" align="right">
        <template #default="{ row }">{{ money(row.amount) }}</template>
      </el-table-column>
      <el-table-column label="返款" width="120">
        <template #default="{ row }">
          <el-switch :model-value="row.refunded === 1" :disabled="!auth.canEdit" @change="(v) => onRefundToggle(row, v)" />
          <div class="muted" v-if="row.refundedAt">{{ row.refundedAt?.slice(0, 10) }} {{ row.refundedBy }}</div>
        </template>
      </el-table-column>
      <el-table-column label="原因" width="90" prop="reasonName" />
      <el-table-column label="截图" width="110">
        <template #default="{ row }">
          <template v-if="row.attachments?.length">
            <el-image v-for="att in row.attachments.slice(0, 2)" :key="att.id" :src="attachmentUrl(att.id)"
              fit="cover" class="thumb" :preview-src-list="row.attachments.map((a) => attachmentUrl(a.id))" preview-teleported />
            <span v-if="row.attachments.length > 2" class="muted">+{{ row.attachments.length - 2 }}</span>
          </template>
          <span v-else class="muted">无</span>
        </template>
      </el-table-column>
      <el-table-column label="备注" min-width="110" prop="remark" show-overflow-tooltip />
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" :disabled="!auth.canEdit" @click="openSupEdit(row)">编辑</el-button>
          <el-button link type="danger" :disabled="!auth.canEdit" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 推广费表格 -->
    <el-table v-if="tab === 'promotion'" v-loading="loading" :data="items" border stripe size="small">
      <el-table-column label="日期" width="105" prop="bizDate" />
      <el-table-column label="平台" width="100" prop="platformName" />
      <el-table-column label="店铺" min-width="140" prop="shopName" />
      <el-table-column label="负责人" width="90" prop="ownerName" />
      <el-table-column label="公司主体" min-width="170" prop="companyName" show-overflow-tooltip />
      <el-table-column label="推广类型" width="110" prop="promoTypeName" />
      <el-table-column label="金额" width="110" align="right">
        <template #default="{ row }">{{ money(row.amount) }}</template>
      </el-table-column>
      <el-table-column label="备注" min-width="120" prop="remark" show-overflow-tooltip />
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" :disabled="!auth.canEdit" @click="openPromoEdit(row)">编辑</el-button>
          <el-button link type="danger" :disabled="!auth.canEdit" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      style="margin-top: 14px; justify-content: flex-end"
      layout="total, sizes, prev, pager, next"
      :total="total"
      v-model:current-page="page"
      v-model:page-size="pageSize"
      :page-sizes="[20, 50, 100]"
      @change="loadPage()"
    />

    <!-- 销售编辑 -->
    <el-dialog v-model="saleDialog" title="编辑销售记录" width="420px">
      <el-form label-width="90px" v-if="saleForm">
        <el-form-item label="店铺"><span>{{ saleForm.shopName }}</span><span class="muted">（{{ saleForm.bizDate }}）</span></el-form-item>
        <el-form-item label="销售额">
          <el-input-number v-model="saleForm.saleAmount" :min="0" :precision="2" :controls="false" style="width: 100%" />
        </el-form-item>
        <el-form-item label="退款金额">
          <el-input-number v-model="saleForm.refundAmount" :min="0" :precision="2" :controls="false" style="width: 100%" />
        </el-form-item>
        <el-form-item label="备注"><el-input v-model="saleForm.remark" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="saleDialog = false">取 消</el-button>
        <el-button type="primary" @click="saveSaleEdit">保 存</el-button>
      </template>
    </el-dialog>

    <!-- 补单编辑 -->
    <el-dialog v-model="supDialog" title="编辑补单记录" width="480px">
      <el-form label-width="90px" v-if="supForm">
        <el-form-item label="日期">
          <el-date-picker v-model="supForm.bizDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" :clearable="false" />
        </el-form-item>
        <el-form-item label="店铺">
          <el-select v-model="supForm.shopId" filterable style="width: 100%">
            <el-option v-for="s in shops" :key="s.id" :label="s.name" :value="s.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="订单号"><el-input v-model="supForm.orderNo" /></el-form-item>
        <el-form-item label="下单人">
          <el-select v-model="supForm.ordererId" filterable style="width: 100%">
            <el-option v-for="o in orderers" :key="o.id" :label="o.name" :value="o.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="金额">
          <el-input-number v-model="supForm.amount" :min="0.01" :precision="2" :controls="false" style="width: 100%" />
        </el-form-item>
        <el-form-item label="补单原因">
          <el-select v-model="supForm.reasonCode" clearable style="width: 100%">
            <el-option v-for="r in reasons" :key="r.itemCode" :label="r.itemName" :value="r.itemCode" />
          </el-select>
        </el-form-item>
        <el-form-item label="备注"><el-input v-model="supForm.remark" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="supDialog = false">取 消</el-button>
        <el-button type="primary" @click="saveSupEdit">保 存</el-button>
      </template>
    </el-dialog>

    <!-- 推广费编辑 -->
    <el-dialog v-model="promoDialog" title="编辑推广费" width="440px">
      <el-form label-width="90px" v-if="promoForm">
        <el-form-item label="日期">
          <el-date-picker v-model="promoForm.bizDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" :clearable="false" />
        </el-form-item>
        <el-form-item label="店铺">
          <el-select v-model="promoForm.shopId" filterable style="width: 100%">
            <el-option v-for="s in shops" :key="s.id" :label="s.name" :value="s.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="推广类型">
          <el-select v-model="promoForm.promoTypeCode" style="width: 100%">
            <el-option v-for="t in promoTypes" :key="t.itemCode" :label="t.itemName" :value="t.itemCode" />
          </el-select>
        </el-form-item>
        <el-form-item label="金额">
          <el-input-number v-model="promoForm.amount" :min="0.01" :precision="2" :controls="false" style="width: 100%" />
        </el-form-item>
        <el-form-item label="备注"><el-input v-model="promoForm.remark" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="promoDialog = false">取 消</el-button>
        <el-button type="primary" @click="savePromoEdit">保 存</el-button>
      </template>
    </el-dialog>

    <!-- 导入对话框 -->
    <el-dialog v-model="importDialog" title="批量导入" width="520px">
      <el-alert type="info" :closable="false" style="margin-bottom: 12px"
        :title="`当前导入：${tabName}。请先下载模板按格式填写；重复记录处理方式可选。`" />
      <div class="filter-bar">
        <el-button @click="downloadTemplate">下载导入模板</el-button>
        <el-select v-model="importMode" style="width: 160px">
          <el-option label="重复记录跳过" value="skip" />
          <el-option label="重复记录覆盖" value="overwrite" />
        </el-select>
        <input ref="fileInput" type="file" accept=".xlsx,.xls" style="display: none" @change="onImportFile" />
        <el-button type="primary" @click="fileInput?.click()">选择 Excel 文件</el-button>
      </div>
      <div v-if="importResult">
        <el-divider content-position="left">导入结果</el-divider>
        <p>
          新增 <b style="color: #67c23a">{{ importResult.inserted }}</b> 条，
          覆盖更新 <b style="color: #e6a23c">{{ importResult.updated }}</b> 条，
          跳过 <b>{{ importResult.skipped }}</b> 条，
          错误 <b style="color: #f56c6c">{{ importResult.errors?.length || 0 }}</b> 条
        </p>
        <el-alert v-for="(e, i) in (importResult.errors || []).slice(0, 20)" :key="i" :title="e" type="error" :closable="false" class="err-item" />
        <div v-if="(importResult.errors?.length || 0) > 20" class="muted">…… 其余 {{ importResult.errors.length - 20 }} 条错误已省略</div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  attachmentUrl, deletePromotion, deleteSale, deleteSupplement,
  getDicts, getMasters, getPromotions, getSales, getShops, getSupplements,
  setSupplementRefund, updatePromotion, updateSale, updateSupplement
} from '../api'
import { download } from '../api/request'
import { useAuthStore } from '../stores/auth'
import { money, today } from '../utils/format'
import dayjs from 'dayjs'

const auth = useAuthStore()
const tab = ref('sale')
const range = ref([dayjs().startOf('month').format('YYYY-MM-DD'), today()])
const filters = ref({})
const items = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)

const shops = ref([])
const owners = ref([])
const companies = ref([])
const orderers = ref([])
const platforms = ref([])
const promoTypes = ref([])
const reasons = ref([])

const saleDialog = ref(false)
const saleForm = ref(null)
const supDialog = ref(false)
const supForm = ref(null)
const promoDialog = ref(false)
const promoForm = ref(null)

const importDialog = ref(false)
const importMode = ref('skip')
const importResult = ref(null)
const fileInput = ref(null)

const tabName = computed(() => ({ sale: '销售明细', supplement: '补单明细', promotion: '推广费明细' }[tab.value]))

const listParams = () => ({
  from: range.value?.[0],
  to: range.value?.[1],
  ...filters.value,
  page: page.value,
  pageSize: pageSize.value
})

async function loadBase() {
  ;[shops.value, owners.value, companies.value, orderers.value, platforms.value, promoTypes.value, reasons.value] =
    await Promise.all([
      getShops({ status: 1 }),
      getMasters('owner'),
      getMasters('company'),
      getMasters('orderer'),
      getDicts('platform'),
      getDicts('promo_type'),
      getDicts('supplement_reason')
    ])
}

async function loadPage(p) {
  if (p) page.value = p
  loading.value = true
  try {
    let data
    if (tab.value === 'sale') data = await getSales(listParams())
    else if (tab.value === 'supplement') data = await getSupplements(listParams())
    else data = await getPromotions(listParams())
    items.value = data.items
    total.value = data.total
  } finally {
    loading.value = false
  }
}

function onReset() {
  range.value = [dayjs().startOf('month').format('YYYY-MM-DD'), today()]
  filters.value = {}
  loadPage(1)
}

// ---------- 编辑 / 删除 ----------
function openSaleEdit(row) {
  saleForm.value = { ...row }
  saleDialog.value = true
}
async function saveSaleEdit() {
  await updateSale(saleForm.value.id, {
    saleAmount: Number(saleForm.value.saleAmount),
    refundAmount: Number(saleForm.value.refundAmount),
    remark: saleForm.value.remark || null
  })
  ElMessage.success('已保存')
  saleDialog.value = false
  loadPage()
}

function openSupEdit(row) {
  supForm.value = { ...row }
  supDialog.value = true
}
async function saveSupEdit() {
  const f = supForm.value
  await updateSupplement(f.id, {
    bizDate: f.bizDate,
    shopId: f.shopId,
    orderNo: f.orderNo || null,
    ordererId: f.ordererId,
    amount: Number(f.amount),
    refunded: f.refunded,
    reasonCode: f.reasonCode || null,
    remark: f.remark || null
  })
  ElMessage.success('已保存')
  supDialog.value = false
  loadPage()
}

function openPromoEdit(row) {
  promoForm.value = { ...row }
  promoDialog.value = true
}
async function savePromoEdit() {
  const f = promoForm.value
  await updatePromotion(f.id, {
    bizDate: f.bizDate,
    shopId: f.shopId,
    promoTypeCode: f.promoTypeCode,
    amount: Number(f.amount),
    remark: f.remark || null
  })
  ElMessage.success('已保存')
  promoDialog.value = false
  loadPage()
}

async function onDelete(row) {
  await ElMessageBox.confirm('确认删除该条记录？删除后不可恢复', '删除确认', { type: 'warning' })
  if (tab.value === 'sale') await deleteSale(row.id)
  else if (tab.value === 'supplement') await deleteSupplement(row.id)
  else await deletePromotion(row.id)
  ElMessage.success('已删除')
  loadPage()
}

async function onRefundToggle(row, v) {
  await setSupplementRefund(row.id, v ? 1 : 0)
  ElMessage.success(v ? '已标记返款（留痕）' : '已取消返款标记')
  loadPage()
}

// ---------- 导入导出 ----------
function onImport() {
  importResult.value = null
  importDialog.value = true
}
function downloadTemplate() {
  download(`/import/template/${tab.value}`)
}
async function onImportFile(e) {
  const file = e.target.files?.[0]
  if (!file) return
  const fd = new FormData()
  fd.append('file', file)
  try {
    importResult.value = await request(`/import/${tab.value}?mode=${importMode.value}`, {
      method: 'POST',
      data: fd,
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    if ((importResult.value.errors?.length || 0) === 0) ElMessage.success('导入完成，无错误')
    loadPage(1)
  } finally {
    e.target.value = ''
  }
}
function onExport() {
  download(`/export/${tab.value}`, listParams())
}

import request from '../api/request'

onMounted(async () => {
  await loadBase()
  await loadPage(1)
})
</script>

<style scoped>
.thumb {
  width: 34px;
  height: 34px;
  border-radius: 4px;
  margin-right: 4px;
  vertical-align: middle;
}
.err-item {
  margin-top: 6px;
}
</style>
