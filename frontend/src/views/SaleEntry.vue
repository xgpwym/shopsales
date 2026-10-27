<template>
  <div class="entry-layout">
    <!-- 左侧：录入表单 -->
    <div class="page-card">
      <div class="filter-bar">
        <span class="field-label">销售日期</span>
        <el-date-picker v-model="form.bizDate" type="date" value-format="YYYY-MM-DD" :clearable="false" :disabled-date="(d) => d.getTime() > Date.now()" @change="loadShops" />
        <span class="field-label">负责人</span>
        <el-select v-model="form.ownerId" filterable placeholder="请选择负责人" style="width: 180px" @change="loadShops">
          <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
        </el-select>
        <el-tag v-if="hasExisting" type="warning">该日期已存在录入记录，保存将覆盖原值</el-tag>
        <div style="flex: 1"></div>
        <el-button type="primary" :loading="saving" :disabled="!auth.canEdit" @click="onSave">保 存</el-button>
        <el-button @click="onReset">重 置</el-button>
      </div>

      <el-alert v-if="form.ownerId && !rows.length" type="info" :closable="false"
        title="该负责人名下没有可录入的店铺（当日归属生效且启用的店铺），请检查店铺管理中的归属版本" />

      <el-table v-if="rows.length" :data="rows" border stripe size="default">
        <el-table-column label="平台" width="110">
          <template #default="{ row }">{{ row.platformName }}</template>
        </el-table-column>
        <el-table-column label="店铺名称" min-width="160" prop="shopName" />
        <el-table-column label="销售额" width="200">
          <template #default="{ row }">
            <el-input-number v-model="row.saleAmount" :min="0" :precision="2" :step="100" :controls="false" style="width: 100%" placeholder="0.00" />
          </template>
        </el-table-column>
        <el-table-column label="退款金额" width="200">
          <template #default="{ row }">
            <el-input-number v-model="row.refundAmount" :min="0" :precision="2" :step="50" :controls="false" style="width: 100%" placeholder="0.00" />
          </template>
        </el-table-column>
        <el-table-column label="公司主体" min-width="180">
          <template #default="{ row }">
            <span>{{ row.companyName }}</span>
            <div class="muted">归属快照以录入日期为准</div>
          </template>
        </el-table-column>
        <el-table-column label="备注" min-width="150">
          <template #default="{ row }">
            <el-input v-model="row.remark" placeholder="备注（选填）" />
          </template>
        </el-table-column>
      </el-table>

      <div class="total-bar" v-if="rows.length">
        <span>合计销售额：¥{{ money(totalSale) }}</span>
        <span>合计退款：¥{{ money(totalRefund) }}</span>
        <span>净销售额：¥{{ money(totalSale - totalRefund) }}</span>
      </div>
    </div>

    <!-- 右侧：最近录入记录 -->
    <div class="page-card recent-panel">
      <h3 class="panel-title">最近录入记录（30 条）</h3>
      <div class="filter-bar">
        <el-input v-model="recentShopKeyword" placeholder="店铺名称过滤" clearable size="small" />
        <el-select v-model="recentOwnerId" placeholder="全部负责人" clearable size="small" @change="loadRecent">
          <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
        </el-select>
      </div>
      <el-table
        v-loading="recentLoading"
        :data="filteredRecent"
        size="small"
        highlight-current-row
        height="calc(100vh - 260px)"
        @row-click="onPickRecent"
      >
        <el-table-column label="日期" width="100" prop="bizDate" />
        <el-table-column label="店铺" min-width="130" prop="shopName" show-overflow-tooltip />
        <el-table-column label="销售额" width="90" align="right">
          <template #default="{ row }">{{ money(row.saleAmount) }}</template>
        </el-table-column>
        <el-table-column label="退款" width="90" align="right">
          <template #default="{ row }">{{ money(row.refundAmount) }}</template>
        </el-table-column>
        <el-table-column label="负责人" width="80" prop="ownerName" />
      </el-table>
      <div class="muted" style="margin-top: 6px">点击任意记录可快速定位并修改该日数据</div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import dayjs from 'dayjs'
import { ElMessage } from 'element-plus'
import { getMasters, getShopsForEntry, getRecentSales, saveSalesBatch } from '../api'
import { useAuthStore } from '../stores/auth'
import { money, yesterday } from '../utils/format'

const auth = useAuthStore()
const owners = ref([])
const form = ref({ bizDate: yesterday(), ownerId: null })
const rows = ref([])
const saving = ref(false)
const hasExisting = ref(false)

const recent = ref([])
const recentLoading = ref(false)
const recentOwnerId = ref(null)
const recentShopKeyword = ref('')

const totalSale = computed(() => rows.value.reduce((s, r) => s + Number(r.saleAmount || 0), 0))
const totalRefund = computed(() => rows.value.reduce((s, r) => s + Number(r.refundAmount || 0), 0))

const filteredRecent = computed(() => {
  const kw = recentShopKeyword.value?.trim()
  if (!kw) return recent.value
  return recent.value.filter((r) => r.shopName.includes(kw))
})

async function loadOwners() {
  owners.value = await getMasters('owner')
}

async function loadShops() {
  if (!form.value.ownerId || !form.value.bizDate) return
  const data = await getShopsForEntry(form.value.ownerId, form.value.bizDate)
  rows.value = data.map((d) => ({
    shopId: d.shopId,
    shopName: d.shopName,
    platformName: d.platformName,
    companyName: d.companyName,
    saleAmount: d.existingSaleAmount || null,
    refundAmount: d.existingRefundAmount || null,
    remark: d.existingRemark || ''
  }))
  hasExisting.value = data.some((d) => d.existingSaleAmount > 0 || d.existingRefundAmount > 0)
}

async function loadRecent() {
  recentLoading.value = true
  try {
    recent.value = await getRecentSales({ limit: 30, ownerId: recentOwnerId.value || undefined })
  } finally {
    recentLoading.value = false
  }
}

async function onSave() {
  if (!form.value.bizDate || !form.value.ownerId) return ElMessage.warning('请选择日期和负责人')
  const items = rows.value
    .filter((r) => Number(r.saleAmount) > 0 || Number(r.refundAmount) > 0)
    .map((r) => ({ shopId: r.shopId, saleAmount: Number(r.saleAmount || 0), refundAmount: Number(r.refundAmount || 0), remark: r.remark || null }))
  if (!items.length) return ElMessage.warning('请至少填写一个店铺的销售额或退款金额')

  saving.value = true
  try {
    await saveSalesBatch({ bizDate: form.value.bizDate, items })
    ElMessage.success(`已保存 ${form.value.bizDate} 共 ${items.length} 家店铺的数据`)
    await Promise.all([loadRecent(), loadShops()])
  } finally {
    saving.value = false
  }
}

function onReset() {
  form.value = { bizDate: yesterday(), ownerId: null }
  rows.value = []
  hasExisting.value = false
}

/** 点击右侧记录 → 快速定位到该天该负责人的录入 */
async function onPickRecent(row) {
  form.value.bizDate = row.bizDate
  form.value.ownerId = row.ownerId
  await loadShops()
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

onMounted(async () => {
  await loadOwners()
  await loadRecent()
})
</script>

<style scoped>
.entry-layout {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}
.entry-layout > .page-card:first-child {
  flex: 1;
  min-width: 0;
}
.recent-panel {
  width: 520px;
  flex-shrink: 0;
}
.field-label {
  color: #606266;
  font-size: 14px;
}
</style>
