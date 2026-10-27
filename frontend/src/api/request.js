import axios from 'axios'
import { ElMessage } from 'element-plus'

const request = axios.create({ baseURL: '/api', timeout: 60000 })

request.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

request.interceptors.response.use(
  (res) => res.data,
  (err) => {
    const msg = err.response?.data?.message || err.message || '请求失败'
    if (err.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      if (location.pathname !== '/login') location.href = '/login'
    } else {
      ElMessage.error(msg)
    }
    return Promise.reject(err)
  }
)

/** 下载文件（带鉴权，走 blob） */
export async function download(url, params) {
  const token = localStorage.getItem('token')
  const res = await axios.get(url, {
    params,
    responseType: 'blob',
    baseURL: '/api',
    headers: token ? { Authorization: `Bearer ${token}` } : {}
  })
  const disposition = res.headers['content-disposition'] || ''
  const match = disposition.match(/filename\*?=(?:UTF-8''|")?([^\";]+)/i)
  let filename = match ? decodeURIComponent(match[1]) : '导出文件.xlsx'
  const blob = new Blob([res.data])
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = filename
  a.click()
  URL.revokeObjectURL(a.href)
}

export default request
