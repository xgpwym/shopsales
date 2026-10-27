import dayjs from 'dayjs'

export function money(v) {
  if (v == null) return '—'
  return Number(v).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function yesterday() {
  return dayjs().subtract(1, 'day').format('YYYY-MM-DD')
}

export function today() {
  return dayjs().format('YYYY-MM-DD')
}

export function pct(v) {
  return v == null ? '—' : `${v}%`
}

export function momText(v) {
  if (v == null) return '—'
  return v >= 0 ? `↑${v}%` : `↓${Math.abs(v)}%`
}

export function momClass(v) {
  if (v == null) return ''
  return v >= 0 ? 'amount-up' : 'amount-down'
}
