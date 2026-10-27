<template>
  <div class="entry-layout">
    <!-- 左侧：录入表单 -->
    <div class="page-card">
      <h3 class="panel-title">{{ editingId ? '编辑推广费' : '推广费录入' }}</h3>
      <el-form :model="form" label-width="90px" style="max-width: 520px">
        <el-form-item label="投放日期" required>
          <el-date-picker v-model="form.bizDate" type="date" value-format="YYYY-MM-DD" :clearable="false" style="width: 100%"
            :disabled-date="(d) => d.getTime() > Date.now()" />
        </el-form-item>
        <el-form-item label="店铺" required>
          <el-select v-model="form.shopId" filterable placeholder="选择店铺" style="width: 100%">
            <el-option v-for="s in shops" :key="s.id" :value="s.id" :label="`${s.platformName} - ${s.name}`" />
          </el-select>
        </el-form-item>
        <el-form-item label="推广类型" required>
          <el-select v-model="form.promoTypeCode" placeholder="选择推广类型" style="width: 100%">
            <el-option v-for="t in promoTypes" :key="t.itemCode" :label="t.itemName" :value="t.itemCode" />
          </el-select>
        </el-form-item>
        <el-form-item label="金额" required>
          <el-input-number v-model="form.amount" :min="0" :precision="2" :controls="false" style="width: 100%" placeholder="0.00" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" placeholder="备注（选填）" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="saving" :disabled="!auth.canEdit" @click="onSave">
            {{ editingId ? '保存修改' : '保 存' }}
          </el-button>
          <el-button v-if="editingId" @click="resetForm">取消编辑</el-button>
          <div class="muted" style="margin-left: 12px">同一店铺同日同推广类型重复保存将覆盖原值</div>
        </el-form-item>
      </el-form>
    </div>

    <!-- 右侧：最近推广费记录 -->
    <div class="page-card recent-panel">
      <h3 class="panel-title">最近推广费记录（30 条）</h3>
      <el-table v-loading="recentLoading" :data="recent" size="small" highlight-current-row height="calc(100vh - 230px)" @row-click="onPickRecent">
        <el-table-column label="日期" width="100" prop="bizDate" />
        <el-table-column label="店铺" min-width="120" prop="shopName" show-overflow-tooltip />
        <el-table-column label="类型" width="90" prop="promoTypeName" />
        <el-table-column label="金额" width="100" align="right">
          <template #default="{ row }">{{ money(row.amount) }}</template>
        </el-table-column>
      </el-table>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getDicts, getMasters, getPromotions, getShops, savePromotion, updatePromotion } from '../api'
import { useAuthStore } from '../stores/auth'
import { money, yesterday } from '../utils/format'

const auth = useAuthStore()
const shops = ref([])
const promoTypes = ref([])
const form = ref(defaultForm())
const saving = ref(false)
const editingId = ref(null)
const recent = ref([])
const recentLoading = ref(false)

function defaultForm() {
  return { bizDate: yesterday(), shopId: null, promoTypeCode: null, amount: null, remark: '' }
}

async function loadBase() {
  ;[shops.value, promoTypes.value] = await Promise.all([getShops({ status: 1 }), getDicts('promo_type')])
}

async function loadRecent() {
  recentLoading.value = true
  try {
    const data = await getPromotions({ page: 1, pageSize: 30 })
    recent.value = data.items
  } finally {
    recentLoading.value = false
  }
}

async function onSave() {
  const f = form.value
  if (!f.bizDate || !f.shopId || !f.promoTypeCode) return ElMessage.warning('请填写日期、店铺、推广类型')
  if (!f.amount || f.amount <= 0) return ElMessage.warning('请填写大于 0 的金额')

  saving.value = true
  try {
    const payload = {
      bizDate: f.bizDate,
      shopId: f.shopId,
      promoTypeCode: f.promoTypeCode,
      amount: Number(f.amount),
      remark: f.remark || null
    }
    if (editingId.value) {
      await updatePromotion(editingId.value, payload)
      ElMessage.success('修改已保存')
    } else {
      const res = await savePromotion(payload)
      ElMessage.success(res.inserted ? '推广费已保存' : '该日该类型已有记录，已覆盖原值')
    }
    resetForm()
    await loadRecent()
  } finally {
    saving.value = false
  }
}

async function onPickRecent(row) {
  editingId.value = row.id
  form.value = {
    bizDate: row.bizDate,
    shopId: row.shopId,
    promoTypeCode: row.promoTypeCode,
    amount: Number(row.amount),
    remark: row.remark || ''
  }
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function resetForm() {
  form.value = defaultForm()
  editingId.value = null
}

onMounted(async () => {
  await loadBase()
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
  width: 440px;
  flex-shrink: 0;
}
</style>
