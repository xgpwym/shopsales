<template>
  <div v-loading="loading">
    <el-row :gutter="16">
      <el-col :span="6" v-for="card in cards" :key="card.label">
        <div class="page-card stat-card">
          <div class="stat-label">{{ card.label }}</div>
          <div class="stat-value">¥ {{ money(card.value) }}</div>
          <div v-if="card.extra" class="muted">{{ card.extra }}</div>
        </div>
      </el-col>
    </el-row>

    <el-row :gutter="16" style="margin-top: 16px">
      <el-col :span="14">
        <div class="page-card">
          <h3 class="panel-title">近 30 天净销售额趋势</h3>
          <div ref="trendRef" class="chart chart-lg"></div>
        </div>
      </el-col>
      <el-col :span="10">
        <div class="page-card">
          <h3 class="panel-title">本月推广费按平台占比</h3>
          <div ref="pieRef" class="chart chart-lg"></div>
        </div>
      </el-col>
    </el-row>

    <el-row style="margin-top: 16px">
      <el-col :span="24">
        <div class="page-card">
          <h3 class="panel-title">本月店铺净销售额排行 Top 10</h3>
          <div ref="barRef" class="chart chart-sm"></div>
        </div>
      </el-col>
    </el-row>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import * as echarts from 'echarts'
import { getDashboard } from '../api'
import { money, momText } from '../utils/format'

const loading = ref(true)
const data = ref(null)
const trendRef = ref()
const pieRef = ref()
const barRef = ref()
let charts = []

const cards = computed(() => {
  const c = data.value?.cards || {}
  return [
    { label: '今日净销售额', value: c.todayNet ?? 0 },
    { label: '本月净销售额', value: c.monthNet ?? 0, extra: c.monthMom != null ? `环比上月 ${momText(c.monthMom)}` : '' },
    { label: '本月推广费', value: c.monthPromo ?? 0 },
    { label: '未返款补单金额', value: c.unrefunded ?? 0 }
  ]
})

function initCharts() {
  const trend = echarts.init(trendRef.value)
  trend.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: 60, right: 20, top: 20, bottom: 30 },
    xAxis: { type: 'category', data: data.value.trend.map((t) => t.date) },
    yAxis: { type: 'value' },
    series: [
      {
        name: '净销售额',
        type: 'line',
        smooth: true,
        areaStyle: { opacity: 0.15 },
        data: data.value.trend.map((t) => t.net)
      }
    ]
  })
  charts.push(trend)

  const pie = echarts.init(pieRef.value)
  pie.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: ¥{c} ({d}%)' },
    legend: { bottom: 0 },
    series: [
      {
        type: 'pie',
        radius: ['38%', '62%'],
        center: ['50%', '45%'],
        data: (data.value.promoByPlatform || []).map((p) => ({ name: p.name, value: p.amount }))
      }
    ]
  })
  charts.push(pie)

  const bar = echarts.init(barRef.value)
  const tops = [...(data.value.topShops || [])].reverse()
  bar.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: 120, right: 30, top: 10, bottom: 30 },
    xAxis: { type: 'value' },
    yAxis: { type: 'category', data: tops.map((t) => t.name) },
    series: [{ type: 'bar', data: tops.map((t) => t.net), itemStyle: { color: '#409eff' } }]
  })
  charts.push(bar)

  const onResize = () => charts.forEach((c) => c.resize())
  window.addEventListener('resize', onResize)
  onBeforeUnmount(() => window.removeEventListener('resize', onResize))
}

onMounted(async () => {
  try {
    data.value = await getDashboard()
  } finally {
    loading.value = false
  }
  initCharts()
})

onBeforeUnmount(() => charts.forEach((c) => c.dispose()))
</script>

<style scoped>
.stat-card .stat-label {
  color: #909399;
  font-size: 13px;
}
.stat-card .stat-value {
  font-size: 24px;
  font-weight: 700;
  color: #303133;
  margin-top: 6px;
}
.chart {
  width: 100%;
}
.chart-lg {
  height: 300px;
}
.chart-sm {
  height: 320px;
}
</style>
