<template>
  <div class="entry-layout" ref="pasteZone" tabindex="0">
    <!-- 左侧：录入表单 -->
    <div class="page-card">
      <h3 class="panel-title">{{ editingId ? '编辑补单记录' : '补单录入' }} <span class="muted">（支持 Ctrl+V 粘贴订单截图）</span></h3>
      <el-form :model="form" label-width="90px" style="max-width: 620px">
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="补单日期" required>
              <el-date-picker v-model="form.bizDate" type="date" value-format="YYYY-MM-DD" :clearable="false" style="width: 100%"
                :disabled-date="(d) => d.getTime() > Date.now()" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="店铺" required>
              <el-select v-model="form.shopId" filterable placeholder="选择店铺" style="width: 100%" @change="onShopChange">
                <el-option v-for="s in shops" :key="s.id" :value="s.id" :label="`${s.platformName} - ${s.name}`">
                  <span>{{ s.name }}</span>
                  <span class="muted" style="float: right">{{ s.platformName }} · {{ s.ownerName }} · {{ s.companyName }}</span>
                </el-option>
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="订单号">
              <el-input v-model="form.orderNo" placeholder="平台订单号（选填）" clearable />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="下单人" required>
              <el-select v-model="form.ordererId" filterable placeholder="选择下单人" style="width: 100%">
                <el-option v-for="o in orderers" :key="o.id" :label="o.name" :value="o.id" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="金额" required>
              <el-input-number v-model="form.amount" :min="0" :precision="2" :controls="false" style="width: 100%" placeholder="0.00" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="补单原因">
              <el-select v-model="form.reasonCode" placeholder="选择原因（选填）" clearable style="width: 100%">
                <el-option v-for="r in reasons" :key="r.itemCode" :label="r.itemName" :value="r.itemCode" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="是否返款">
              <el-switch v-model="form.refunded" active-text="已返款" inactive-text="未返款" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="备注">
              <el-input v-model="form.remark" placeholder="备注（选填）" />
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item label="订单截图">
          <div class="att-tip">点击/拖拽/粘贴上传，可多张（jpg/png/webp/gif，单张 ≤5MB）</div>
          <el-upload
            :auto-upload="false"
            :show-file-list="false"
            accept="image/*"
            multiple
            :on-change="onFileChange"
          >
            <el-button size="small" type="primary" plain>选择图片</el-button>
          </el-upload>
          <div class="att-list">
            <!-- 已上传的附件 -->
            <div v-for="att in existingAttachments" :key="att.id" class="att-item">
              <el-image :src="attachmentUrl(att.id)" fit="cover" :preview-src-list="[attachmentUrl(att.id)]" preview-teleported />
              <el-icon class="att-del" @click="onDelAttachment(att)"><CircleCloseFilled /></el-icon>
            </div>
            <!-- 待上传的新图 -->
            <div v-for="(f, idx) in pendingFiles" :key="idx" class="att-item">
              <el-image :src="f.previewUrl" fit="cover" :preview-src-list="[f.previewUrl]" preview-teleported />
              <el-icon class="att-del" @click="pendingFiles.splice(idx, 1)"><CircleCloseFilled /></el-icon>
            </div>
          </div>
        </el-form-item>

        <el-form-item>
          <el-button type="primary" :loading="saving" :disabled="!auth.canEdit" @click="onSave">
            {{ editingId ? '保存修改' : '保 存' }}
          </el-button>
          <el-button v-if="editingId" @click="resetForm">取消编辑</el-button>
        </el-form-item>
      </el-form>
    </div>

    <!-- 右侧：最近补单记录 -->
    <div class="page-card recent-panel">
      <h3 class="panel-title">最近补单记录（30 条）</h3>
      <el-table v-loading="recentLoading" :data="recent" size="small" highlight-current-row height="calc(100vh - 230px)" @row-click="onPickRecent">
        <el-table-column label="日期" width="100" prop="bizDate" />
        <el-table-column label="店铺" min-width="120" prop="shopName" show-overflow-tooltip />
        <el-table-column label="金额" width="90" align="right">
          <template #default="{ row }">{{ money(row.amount) }}</template>
        </el-table-column>
        <el-table-column label="下单人" width="80" prop="ordererName" />
        <el-table-column label="返款" width="70">
          <template #default="{ row }">
            <el-tag :type="row.refunded === 1 ? 'success' : 'danger'" size="small">{{ row.refunded === 1 ? '已返' : '未返' }}</el-tag>
          </template>
        </el-table-column>
      </el-table>
      <div class="muted" style="margin-top: 6px">点击记录进入编辑；返款状态可在数据明细中批量勾选</div>
    </div>
  </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  attachmentUrl, createSupplement, deleteAttachment, getDicts, getMasters, getShops,
  getSupplements, updateSupplement, uploadAttachments
} from '../api'
import { useAuthStore } from '../stores/auth'
import { money, yesterday } from '../utils/format'

const auth = useAuthStore()
const shops = ref([])
const owners = ref([])
const orderers = ref([])
const reasons = ref([])

const defaultForm = () => ({
  bizDate: yesterday(),
  shopId: null,
  orderNo: '',
  ordererId: null,
  amount: null,
  refunded: false,
  reasonCode: null,
  remark: ''
})
const form = ref(defaultForm())
const saving = ref(false)
const editingId = ref(null)
const existingAttachments = ref([])
const pendingFiles = ref([])
const pasteZone = ref()

const recent = ref([])
const recentLoading = ref(false)

async function loadBase() {
  ;[shops.value, owners.value, orderers.value, reasons.value] = await Promise.all([
    getShops({ status: 1 }),
    getMasters('owner'),
    getMasters('orderer'),
    getDicts('supplement_reason')
  ])
}

async function loadRecent() {
  recentLoading.value = true
  try {
    const data = await getSupplements({ page: 1, pageSize: 30 })
    recent.value = data.items
  } finally {
    recentLoading.value = false
  }
}

function onShopChange() {
  const shop = shops.value.find((s) => s.id === form.value.shopId)
  if (shop) ElMessage.info(`归属快照按录入日期解析：${shop.ownerName} / ${shop.companyName}`)
}

function addFiles(fileList) {
  for (const f of fileList) {
    if (!f.type?.startsWith('image/')) continue
    if (f.size > 5 * 1024 * 1024) {
      ElMessage.warning(`${f.name} 超过 5MB，已跳过`)
      continue
    }
    pendingFiles.value.push({ file: f, previewUrl: URL.createObjectURL(f) })
  }
}

function onFileChange(uploadFile) {
  addFiles([uploadFile.raw])
}

/** 支持在页面任意位置 Ctrl+V 粘贴图片 */
function onPaste(e) {
  const items = [...(e.clipboardData?.items || [])]
  const imgs = items.filter((i) => i.type.startsWith('image/')).map((i) => i.getAsFile()).filter(Boolean)
  if (imgs.length) {
    addFiles(imgs)
    ElMessage.success(`已从剪贴板添加 ${imgs.length} 张图片`)
  }
}

async function onSave() {
  const f = form.value
  if (!f.bizDate || !f.shopId || !f.ordererId) return ElMessage.warning('请填写日期、店铺、下单人')
  if (!f.amount || f.amount <= 0) return ElMessage.warning('请填写大于 0 的补单金额')

  saving.value = true
  try {
    const payload = {
      bizDate: f.bizDate,
      shopId: f.shopId,
      orderNo: f.orderNo || null,
      ordererId: f.ordererId,
      amount: Number(f.amount),
      refunded: f.refunded ? 1 : 0,
      reasonCode: f.reasonCode || null,
      remark: f.remark || null
    }
    let recordId = editingId.value
    if (editingId.value) {
      await updateSupplement(editingId.value, payload)
      ElMessage.success('修改已保存')
    } else {
      recordId = (await createSupplement(payload)).id
      ElMessage.success('补单已保存')
    }
    // 上传待传图片
    if (pendingFiles.value.length) {
      const fd = new FormData()
      pendingFiles.value.forEach((p) => fd.append('files', p.file))
      await uploadAttachments(recordId, fd)
      ElMessage.success(`已上传 ${pendingFiles.value.length} 张截图`)
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
    orderNo: row.orderNo || '',
    ordererId: row.ordererId,
    amount: Number(row.amount),
    refunded: row.refunded === 1,
    reasonCode: row.reasonCode || null,
    remark: row.remark || ''
  }
  existingAttachments.value = row.attachments || []
  pendingFiles.value = []
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

async function onDelAttachment(att) {
  await deleteAttachment(att.id)
  existingAttachments.value = existingAttachments.value.filter((x) => x.id !== att.id)
  ElMessage.success('截图已删除')
}

function resetForm() {
  form.value = defaultForm()
  editingId.value = null
  existingAttachments.value = []
  pendingFiles.value = []
}

onMounted(async () => {
  await loadBase()
  await loadRecent()
  window.addEventListener('paste', onPaste)
})
onBeforeUnmount(() => window.removeEventListener('paste', onPaste))
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
  width: 480px;
  flex-shrink: 0;
}
.att-tip {
  color: #909399;
  font-size: 12px;
  margin-bottom: 6px;
}
.att-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}
.att-item {
  position: relative;
  width: 72px;
  height: 72px;
  border-radius: 6px;
  overflow: visible;
}
.att-item :deep(.el-image) {
  width: 72px;
  height: 72px;
  border-radius: 6px;
  border: 1px solid #ebeef5;
}
.att-del {
  position: absolute;
  top: -7px;
  right: -7px;
  color: #f56c6c;
  background: #fff;
  border-radius: 50%;
  font-size: 16px;
  cursor: pointer;
  z-index: 2;
}
</style>
