<template>
  <div class="page-card">
    <el-tabs v-model="tab">
      <!-- 多维汇总 -->
      <el-tab-pane label="多维汇总" name="summary">
        <div class="filter-bar">
          <el-date-picker v-model="sRange" type="daterange" value-format="YYYY-MM-DD" start-placeholder="开始日期" end-placeholder="结束日期" style="width: 260px" />
          <el-radio-group v-model="sDim">
            <el-radio-button value="owner">按负责人</el-radio-button>
            <el-radio-button value="company">按公司主体</el-radio-button>
            <el-radio-button value="shop">按店铺</el-radio-button>
            <el-radio-button value="platform">按平台</el-radio-button>
          </el-radio-group>
          <el-select v-model="sMetrics" multiple collapse-tags placeholder="指标" style="width: 250px">
            <el-option v-for="(v, k) in metricNames" :key="k" :label="v" :value="k" />
          </el-select>
          <el-select v-model="sFilters.platform" placeholder="平台" clearable filterable>
            <el-option v-for="p in platforms" :key="p.itemCode" :label="p.itemName" :value="p.itemCode" />
          </el-select>
          <el-select v-model="sFilters.ownerId" placeholder="负责人" clearable filterable>
            <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
          </el-select>
          <el-select v-model="sFilters.companyId" placeholder="公司主体" clearable filterable>
            <el-option v-for="c in companies" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
          <el-select v-model="sFilters.shopId" placeholder="店铺" clearable filterable>
            <el-option v-for="s in shops" :key="s.id" :label="s.name" :value="s.id" />
          </el-select>
          <el-button type="primary" @click="loadSummary">查 询</el-button>
          <el-button @click="onExportSummary">导出 Excel</el-button>
        </div>

        <el-table v-loading="sLoading" :data="sData?.rows || []" border stripe size="small" show-summary :summary-method="summaryMethod">
          <el-table-column label="项目" min-width="150" prop="label" fixed="left" />
          <template v-for="m in sMetrics" :key="m">
            <el-table-column :label="metricNames[m]" align="right" min-width="110">
              <template #default="{ row }">{{ money(row[m]) }}</template>
            </el-table-column>
            <el-table-column label="占比" align="right" width="80">
              <template #default="{ row }">{{ pct(row[m + 'Pct']) }}</template>
            </el-table-column>
            <el-table-column label="环比" align="right" width="90">
              <template #default="{ row }">
                <span :class="momClass(row[m + 'Mom'])">{{ momText(row[m + 'Mom']) }}</span>
              </template>
            </el-table-column>
          </template>
        </el-table>
        <div class="muted" style="margin-top: 8px" v-if="sData">
          统计区间 {{ sData.from }} ~ {{ sData.to }}；环比对比 {{ sData.prevFrom }} ~ {{ sData.prevTo }}；占比 = 当前指标 / 当前筛选范围合计
        </div>
      </el-tab-pane>

      <!-- 月度矩阵 -->
      <el-tab-pane label="月度矩阵" name="monthly">
        <div class="filter-bar">
          <el-date-picker v-model="mYear" type="year" value-format="YYYY" placeholder="年份" :clearable="false" style="width: 120px" />
          <el-radio-group v-model="mDim">
            <el-radio-button value="owner">按负责人</el-radio-button>
            <el-radio-button value="company">按公司主体</el-radio-button>
            <el-radio-button value="shop">按店铺</el-radio-button>
            <el-radio-button value="platform">按平台</el-radio-button>
          </el-radio-group>
          <el-select v-model="mMetric" style="width: 140px">
            <el-option v-for="(v, k) in metricNames" :key="k" :label="v" :value="k" />
          </el-select>
          <el-select v-model="mFilters.platform" placeholder="平台" clearable filterable>
            <el-option v-for="p in platforms" :key="p.itemCode" :label="p.itemName" :value="p.itemCode" />
          </el-select>
          <el-select v-model="mFilters.ownerId" placeholder="负责人" clearable filterable>
            <el-option v-for="o in owners" :key="o.id" :label="o.name" :value="o.id" />
          </el-select>
          <el-button type="primary" @click="loadMonthly">查 询</el-button>
          <el-button @click="onExportMonthly">导出 Excel</el-button>
        </div>

        <el-table v-loading="mLoading" :data="mData?.rows || []" border stripe size="small" show-summary :summary-method="monthlySummary">
          <el-table-column label="项目" min-width="130" prop="label" fixed="left" />
          <el-table-column v-for="mth in 12" :key="mth" :label="`${mth}月`" align="right" min-width="95">
            <template #default="{ row }">
              <span :class="{ 'zero': !row['m' + mth] }">{{ money(row['m' + mth]) }}</span>
            </template>
          </el-table-column>
          <el-table-column label="合计" align="right" min-width="105">
            <template #default="{ row }"><b>{{ money(row.total) }}</b></template>
          </el-table-column>
        </el-table>
      </el-tab-pane>

      <!-- 补单返款汇总 -->
      <el-tab-pane label="补单返款汇总" name="refund">
        <div class="filter-bar">
          <el-date-picker v-model="rRange" type="daterange" value-format="YYYY-MM-DD" start-placeholder="开始日期" end-placeholder="结束日期" style="width: 260px" />
          <el-button type="primary" @click="loadRefund">查 询</el-button>
          <el-button @click="onExportRefund">导出 Excel</el-button>
          <span class="muted">行 = 下单人，列 = 公司主体（按录入时的归属快照），单元格 = 未返款 / 已返款</span>
        </div>

        <el-table v-loading="rLoading" :data="rData?.rows || []" border stripe size="small" show-summary :summary-method="refundSummaryMethod">
          <el-table-column label="下单人" min-width="110" prop="ordererName" fixed="left" />
          <el-table-column v-for="col in rData?.companyCols || []" :key="col.id" :label="col.name" align="center" min-width="150">
            <template #default="{ row }">
              <template v-for="cell in row.cells.filter((c) => c.companyId === col.id)" :key="cell.companyId">
                <div><span style="color: #f56c6c">未返 {{ money(cell.unrefunded) }}</span></div>
                <div><span style="color: #67c23a">已返 {{ money(cell.refunded) }}</span></div>
              </template>
            </template>
          </el-table-column>
          <el-table-column label="未返款合计" align="right" min-width="110">
            <template #default="{ row }"><b style="color: #f56c6c">{{ money(row.unrefunded) }}</b></template>
          </el-table-column>
          <el-table-column label="已返款合计" align="right" min-width="110">
            <template #default="{ row }">{{ money(row.refunded) }}</template>
          </el-table-column>
          <el-table-column label="总金额" align="right" min-width="110">
            <template #default="{ row }"><b>{{ money(row.total) }}</b></template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import dayjs from 'dayjs'
import { ElMessage } from 'element-plus'
import { getDicts, getMasters, getRefundSummary, getReportMonthly, getReportSummary, getShops } from '../api'
import { download } from '../api/request'
import { money, momClass, momText, pct } from '../utils/format'

const metricNames = { sale: '销售额', refund: '退款金额', net: '净销售额', supplement: '补单额', promo: '推广费' }

const tab = ref('summary')
const shops = ref([])
const owners = ref([])
const companies = ref([])
const platforms = ref([])

// 多维汇总
const sRange = ref([dayjs().startOf('month').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')])
const sDim = ref('owner')
const sMetrics = ref(['sale', 'refund', 'net'])
const sFilters = ref({})
const sData = ref(null)
const sLoading = ref(false)

// 月度矩阵
const mYear = ref(dayjs().format('YYYY'))
const mDim = ref('owner')
const mMetric = ref('sale')
const mFilters = ref({})
const mData = ref(null)
const mLoading = ref(false)

// 返款汇总
const rRange = ref([dayjs().startOf('month').subtract(1, 'month').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')])
const rData = ref(null)
const rLoading = ref(false)

async function loadBase() {
  ;[shops.value, owners.value, companies.value, platforms.value] = await Promise.all([
    getShops({ status: 1 }),
    getMasters('owner'),
    getMasters('company'),
    getDicts('platform')
  ])
}

const summaryParams = () => ({
  dim: sDim.value,
  from: sRange.value?.[0],
  to: sRange.value?.[1],
  ...sFilters.value
})
const monthlyParams = (withYear = true) => ({
  dim: mDim.value,
  metric: mMetric.value,
  ...(withYear ? { year: mYear.value } : {}),
  platform: mFilters.value.platform,
  ownerId: mFilters.value.ownerId
})

async function loadSummary() {
  sLoading.value = true
  try {
    sData.value = await getReportSummary(summaryParams())
  } finally {
    sLoading.value = false
  }
}

async function loadMonthly() {
  mLoading.value = true
  try {
    mData.value = await getReportMonthly(monthlyParams())
  } finally {
    mLoading.value = false
  }
}

async function loadRefund() {
  rLoading.value = true
  try {
    rData.value = await getRefundSummary({ from: rRange.value?.[0], to: rRange.value?.[1] })
  } finally {
    rLoading.value = false
  }
}

// 合计行直接使用后端 total
function summaryMethod() {
  const t = sData.value?.total || {}
  const row = [t.label || '合计']
  sMetrics.value.forEach((m) => {
    row.push(money(t[m]))
    row.push(pct(t[m + 'Pct']))
    row.push(momText(t[m + 'Mom']))
  })
  return row
}
function monthlySummary() {
  const t = mData.value?.total || {}
  const row = ['合计']
  for (let i = 1; i <= 12; i++) row.push(money(t['m' + i]))
  row.push(money(t.total))
  return row
}
function refundSummaryMethod() {
  const t = rData.value?.total
  const cols = rData.value?.companyCols || []
  if (!t) return ['合计']
  const row = ['合计']
  const cells = t.cells || []
  cols.forEach((c) => {
    const cell = cells.find((x) => x.companyId === c.id)
    row.push(cell ? `未返 ${money(cell.unrefunded)} / 已返 ${money(cell.refunded)}` : '')
  })
  row.push(money(t.unrefunded), money(t.refunded), money(t.total))
  return row
}

function onExportSummary() {
  download('/report/export', summaryParams())
}
function onExportMonthly() {
  download('/report/export', monthlyParams())
}
function onExportRefund() {
  ElMessage.warning('返款汇总请使用导出明细自行透视；后续版本将支持直接导出')
}

onMounted(async () => {
  await loadBase()
  await Promise.all([loadSummary(), loadMonthly(), loadRefund()])
})
</script>

<style scoped>
.zero {
  color: #c0c4cc;
}
</style>
