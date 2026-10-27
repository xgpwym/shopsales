-- =============================================================
-- 电商销售管理系统 · 演示数据脚本
-- 前置：先执行 init.sql；后端首次启动已写入系统用户
-- 执行：mysql -uroot -p ecom_sales < seed.sql
-- 数据范围：2026-07-01 ~ 2026-10-25（含负责人/主体变更版本演示）
-- =============================================================
USE ecom_sales;

-- ------------------------------------------------------------
-- 字典
-- ------------------------------------------------------------
INSERT INTO sys_dict_item (dict_type, item_code, item_name, sort_no) VALUES
('platform', 'tb',  '淘宝/天猫', 1),
('platform', 'jd',  '京东',      2),
('platform', 'pdd', '拼多多',    3),
('platform', 'dy',  '抖音',      4),
('supplement_reason', 'hdbl', '活动补录', 1),
('supplement_reason', 'clbw', '错漏补录', 2),
('supplement_reason', 'qt',   '其他',     3),
('promo_type', 'ztc',  '直通车',    1),
('promo_type', 'ylmf', '引力魔方',  2),
('promo_type', 'pxb',  '品销宝',    3),
('promo_type', 'zw',   '站外投放',  4),
('promo_type', 'dyll', '抖音引流',  5);

-- ------------------------------------------------------------
-- 负责人 / 公司主体 / 下单人
-- ------------------------------------------------------------
INSERT INTO biz_owner (name, remark) VALUES
('甘金灵', NULL), ('贾梅', NULL), ('吴晓芳', NULL), ('杨柳', NULL), ('张香泽', NULL);

INSERT INTO biz_company (name, credit_code) VALUES
('甘肃呈西优品电子商务有限公司', '91620100MA0000001X'),
('朋达供应链管理有限公司',       '91620100MA0000002X'),
('呈西优品商贸有限公司',         '91620100MA0000003X'),
('凉州农鲜个体经营部',           '92620100MA0000004X');

INSERT INTO sys_orderer (name) VALUES
('库福国'), ('吴晓芳'), ('徐小娟'), ('费梅'), ('杨娜'), ('窦小红');

-- ------------------------------------------------------------
-- 店铺（10 家，覆盖 4 个平台）
-- ------------------------------------------------------------
INSERT INTO biz_shop (name, platform_code, remark) VALUES
('礼遇甘肃',       'tb',  NULL),
('武威特产汇',     'tb',  NULL),
('呈西优品',       'pdd', NULL),
('西北美食特产',   'pdd', NULL),
('武威馆',         'jd',  NULL),
('凉州馆',         'jd',  NULL),
('文创馆',         'jd',  NULL),
('青谷丝路特产馆', 'dy',  NULL),
('青谷西北农特产', 'dy',  NULL),
('甘味优选馆',     'dy',  NULL);

-- 停用一家店铺以演示启用/停用
UPDATE biz_shop SET status = 0 WHERE name = '文创馆';

-- ------------------------------------------------------------
-- 归属版本：初始版本 2026-01-01 生效（负责人：店铺轮转分配；主体同理）
-- ------------------------------------------------------------
INSERT INTO biz_shop_owner_version (shop_id, owner_id, effective_date, created_by)
SELECT id, ((id - 1) MOD 5) + 1, '2026-01-01', 1 FROM biz_shop;

INSERT INTO biz_shop_company_version (shop_id, company_id, effective_date, created_by)
SELECT id, ((id - 1) MOD 4) + 1, '2026-01-01', 1 FROM biz_shop;

-- 变更演示 1：2026-09-01 起两家店铺更换负责人（不影响 9 月前历史数据归属）
INSERT INTO biz_shop_owner_version (shop_id, owner_id, effective_date, created_by) VALUES
((SELECT id FROM (SELECT id FROM biz_shop WHERE name = '礼遇甘肃') t), 4, '2026-09-01', 1),
((SELECT id FROM (SELECT id FROM biz_shop WHERE name = '武威馆') t),   5, '2026-09-01', 1);

-- 变更演示 2：2026-08-15 起武威特产汇更换公司主体
INSERT INTO biz_shop_company_version (shop_id, company_id, effective_date, created_by) VALUES
((SELECT id FROM (SELECT id FROM biz_shop WHERE name = '武威特产汇') t), 1, '2026-08-15', 1);

-- ------------------------------------------------------------
-- 销售登记（逐日生成，含负责人/主体快照；周末上浮；随机缺录）
-- ------------------------------------------------------------
INSERT INTO biz_sale_record (biz_date, shop_id, owner_id, company_id, sale_amount, refund_amount, created_by, created_at)
WITH RECURSIVE dates AS (
  SELECT DATE '2026-07-01' AS dt
  UNION ALL
  SELECT dt + INTERVAL 1 DAY FROM dates WHERE dt < '2026-10-25'
)
SELECT
  d.dt,
  s.id,
  (SELECT v.owner_id   FROM biz_shop_owner_version   v  WHERE v.shop_id  = s.id AND v.effective_date  <= d.dt ORDER BY v.effective_date  DESC, v.id  DESC LIMIT 1),
  (SELECT c.company_id FROM biz_shop_company_version c  WHERE c.shop_id  = s.id AND c.effective_date  <= d.dt ORDER BY c.effective_date  DESC, c.id  DESC LIMIT 1),
  ROUND((800 + ((s.id * 137) MOD 3200)) * (0.7 + RAND() * 0.8) * (CASE WHEN DAYOFWEEK(d.dt) IN (1, 7) THEN 1.25 ELSE 1.00 END), 2),
  ROUND((800 + ((s.id * 137) MOD 3200)) * (0.7 + RAND() * 0.8) * RAND() * 0.12, 2),
  1, NOW()
FROM dates d
JOIN biz_shop s
WHERE s.status = 1
  AND RAND() < 0.90;

-- ------------------------------------------------------------
-- 推广费登记（随机日期×店铺×推广类型）
-- ------------------------------------------------------------
INSERT INTO biz_promotion_record (biz_date, shop_id, owner_id, company_id, promo_type_code, amount, created_by, created_at)
WITH RECURSIVE dates AS (
  SELECT DATE '2026-07-01' AS dt
  UNION ALL
  SELECT dt + INTERVAL 1 DAY FROM dates WHERE dt < '2026-10-25'
),
types AS (
  SELECT 'ztc' AS code UNION ALL SELECT 'ylmf' UNION ALL SELECT 'pxb' UNION ALL SELECT 'zw' UNION ALL SELECT 'dyll'
)
SELECT
  d.dt,
  s.id,
  (SELECT v.owner_id   FROM biz_shop_owner_version   v WHERE v.shop_id = s.id AND v.effective_date <= d.dt ORDER BY v.effective_date DESC, v.id DESC LIMIT 1),
  (SELECT c.company_id FROM biz_shop_company_version c WHERE c.shop_id = s.id AND c.effective_date <= d.dt ORDER BY c.effective_date DESC, c.id DESC LIMIT 1),
  t.code,
  ROUND(80 + RAND() * 480, 2),
  1, NOW()
FROM dates d
JOIN biz_shop s
JOIN types t
WHERE s.status = 1
  AND RAND() < 0.30;

-- ------------------------------------------------------------
-- 补单登记（约 60 条，含返款状态：10 月前默认已返款，10 月部分返款）
-- ------------------------------------------------------------
INSERT INTO biz_supplement_record (biz_date, shop_id, owner_id, company_id, order_no, orderer_id, amount, refunded, refunded_at, refunded_by, reason_code, remark, created_by, created_at)
WITH RECURSIVE dates AS (
  SELECT DATE '2026-07-01' AS dt
  UNION ALL
  SELECT dt + INTERVAL 1 DAY FROM dates WHERE dt < '2026-10-25'
)
SELECT
  d.dt,
  s.id,
  (SELECT v.owner_id   FROM biz_shop_owner_version   v WHERE v.shop_id = s.id AND v.effective_date <= d.dt ORDER BY v.effective_date DESC, v.id DESC LIMIT 1),
  (SELECT c.company_id FROM biz_shop_company_version c WHERE c.shop_id = s.id AND c.effective_date <= d.dt ORDER BY c.effective_date DESC, c.id DESC LIMIT 1),
  CONCAT(
    CASE s.platform_code WHEN 'tb' THEN '3313' WHEN 'jd' THEN '3563' ELSE '2607' END,
    FLOOR(100000000000 + RAND() * 899999999999)
  ),
  (SELECT o.id FROM sys_orderer o ORDER BY RAND() LIMIT 1),
  ROUND(80 + RAND() * 400, 2),
  (CASE WHEN d.dt < '2026-10-01' THEN 1 ELSE IF(RAND() < 0.4, 1, 0) END),
  (CASE WHEN d.dt < '2026-10-01' THEN d.dt + INTERVAL 2 DAY ELSE IF(RAND() < 0.4, d.dt + INTERVAL 2 DAY, NULL) END),
  (CASE WHEN d.dt < '2026-10-01' THEN 1 ELSE IF(RAND() < 0.4, 1, NULL) END),
  ELT(FLOOR(1 + RAND() * 3), 'hdbl', 'clbw', 'qt'),
  NULL,
  1, NOW()
FROM dates d
JOIN biz_shop s
WHERE s.status = 1
  AND RAND() < 0.055;
