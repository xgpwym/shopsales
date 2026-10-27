import request from './request'

// ---------- 认证 ----------
export const login = (data) => request.post('/auth/login', data)
export const getMe = () => request.get('/auth/me')

// ---------- 用户管理 ----------
export const getUsers = () => request.get('/users')
export const createUser = (data) => request.post('/users', data)
export const updateUser = (id, data) => request.put(`/users/${id}`, data)
export const resetPassword = (id, data) => request.put(`/users/${id}/password`, data)
export const deleteUser = (id) => request.delete(`/users/${id}`)

// ---------- 基础资料 ----------
export const getMasters = (kind, all = true) => request.get(`/masters/${kind}`, { params: { all } })
export const createMaster = (kind, data) => request.post(`/masters/${kind}`, data)
export const updateMaster = (kind, id, data) => request.put(`/masters/${kind}/${id}`, data)
export const deleteMaster = (kind, id) => request.delete(`/masters/${kind}/${id}`)
export const getDicts = (type) => request.get(`/masters/dicts/${type}`)
export const createDict = (data) => request.post('/masters/dicts', data)
export const updateDict = (id, data) => request.put(`/masters/dicts/${id}`, data)
export const deleteDict = (id) => request.delete(`/masters/dicts/${id}`)

// ---------- 店铺 ----------
export const getShops = (params) => request.get('/shops', { params })
export const getShopsForEntry = (ownerId, date) => request.get('/shops/for-entry', { params: { ownerId, date } })
export const createShop = (data) => request.post('/shops', data)
export const updateShop = (id, data) => request.put(`/shops/${id}`, data)
export const setShopStatus = (id, status) => request.put(`/shops/${id}/status`, { status })
export const deleteShop = (id) => request.delete(`/shops/${id}`)
export const getOwnerVersions = (id) => request.get(`/shops/${id}/owner-versions`)
export const getCompanyVersions = (id) => request.get(`/shops/${id}/company-versions`)
export const addOwnerVersion = (id, data) => request.post(`/shops/${id}/owner-versions`, data)
export const addCompanyVersion = (id, data) => request.post(`/shops/${id}/company-versions`, data)

// ---------- 销售 ----------
export const getSales = (params) => request.get('/sales', { params })
export const getRecentSales = (params) => request.get('/sales/recent', { params })
export const saveSalesBatch = (data) => request.post('/sales', data)
export const updateSale = (id, data) => request.put(`/sales/${id}`, data)
export const deleteSale = (id) => request.delete(`/sales/${id}`)

// ---------- 补单 ----------
export const getSupplements = (params) => request.get('/supplements', { params })
export const createSupplement = (data) => request.post('/supplements', data)
export const updateSupplement = (id, data) => request.put(`/supplements/${id}`, data)
export const setSupplementRefund = (id, refunded) => request.put(`/supplements/${id}/refund`, { refunded })
export const deleteSupplement = (id) => request.delete(`/supplements/${id}`)
export const uploadAttachments = (id, formData) =>
  request.post(`/supplements/${id}/attachments`, formData, { headers: { 'Content-Type': 'multipart/form-data' } })
export const deleteAttachment = (attId) => request.delete(`/supplements/attachments/${attId}`)
export const attachmentUrl = (id) => `/api/files/${id}`

// ---------- 推广费 ----------
export const getPromotions = (params) => request.get('/promotions', { params })
export const savePromotion = (data) => request.post('/promotions', data)
export const updatePromotion = (id, data) => request.put(`/promotions/${id}`, data)
export const deletePromotion = (id) => request.delete(`/promotions/${id}`)

// ---------- 看板 / 报表 ----------
export const getDashboard = () => request.get('/dashboard')
export const getReportSummary = (params) => request.get('/report/summary', { params })
export const getReportMonthly = (params) => request.get('/report/monthly', { params })
export const getRefundSummary = (params) => request.get('/report/refund-summary', { params })
