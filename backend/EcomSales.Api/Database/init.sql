-- =============================================================
-- 电商销售管理系统 · 表结构（内嵌资源，首次启动时由后端自动执行）
-- 注意：此文件不含 CREATE DATABASE / USE 语句（由 DatabaseInitializer 处理）
-- 手工初始化请使用 database/init.sql
-- =============================================================

SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS biz_supplement_attachment, biz_promotion_record, biz_supplement_record, biz_sale_record,
  biz_shop_owner_version, biz_shop_company_version, biz_shop,
  biz_owner, biz_company, sys_orderer, sys_dict_item, sys_user;
SET FOREIGN_KEY_CHECKS = 1;

-- 系统用户（负责人与登录账号无关，账号仅三种角色）
CREATE TABLE sys_user (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  username      VARCHAR(50)  NOT NULL COMMENT '登录名',
  password_hash VARCHAR(200) NOT NULL COMMENT 'BCrypt 哈希',
  real_name     VARCHAR(50)  NOT NULL DEFAULT '' COMMENT '显示名',
  role          VARCHAR(20)  NOT NULL DEFAULT 'operator' COMMENT 'admin管理员/operator录入员/viewer只读',
  status        INT          NOT NULL DEFAULT 1 COMMENT '1启用 0停用',
  created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_username (username)
) ENGINE = InnoDB COMMENT = '系统用户';

-- 通用字典（平台 / 补单原因 / 推广类型，全部可维护）
CREATE TABLE sys_dict_item (
  id        BIGINT AUTO_INCREMENT PRIMARY KEY,
  dict_type VARCHAR(30) NOT NULL COMMENT 'platform平台/supplement_reason补单原因/promo_type推广类型',
  item_code VARCHAR(30) NOT NULL COMMENT '编码',
  item_name VARCHAR(50) NOT NULL COMMENT '显示名',
  sort_no   INT         NOT NULL DEFAULT 0 COMMENT '排序',
  status    INT         NOT NULL DEFAULT 1 COMMENT '1启用 0停用',
  created_at DATETIME   NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uk_type_code (dict_type, item_code),
  KEY idx_type (dict_type)
) ENGINE = InnoDB COMMENT = '通用字典项';

-- 下单人（独立于负责人档案，用于补单追溯）
CREATE TABLE sys_orderer (
  id         BIGINT AUTO_INCREMENT PRIMARY KEY,
  name       VARCHAR(50) NOT NULL COMMENT '下单人姓名',
  status     INT         NOT NULL DEFAULT 1 COMMENT '1启用 0停用',
  remark     VARCHAR(500) NULL,
  created_at DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME    NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_name (name)
) ENGINE = InnoDB COMMENT = '下单人字典';

-- 负责人（业务档案，与登录账号无关）
CREATE TABLE biz_owner (
  id         BIGINT AUTO_INCREMENT PRIMARY KEY,
  name       VARCHAR(50) NOT NULL COMMENT '负责人姓名',
  status     INT         NOT NULL DEFAULT 1 COMMENT '1在职 0离职',
  remark     VARCHAR(500) NULL,
  created_at DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME    NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_name (name)
) ENGINE = InnoDB COMMENT = '负责人';

-- 公司主体
CREATE TABLE biz_company (
  id          BIGINT AUTO_INCREMENT PRIMARY KEY,
  name        VARCHAR(100) NOT NULL COMMENT '公司主体名称',
  credit_code VARCHAR(60)  NULL COMMENT '统一社会信用代码（可选）',
  status      INT          NOT NULL DEFAULT 1 COMMENT '1启用 0停用',
  remark      VARCHAR(500) NULL,
  created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_name (name)
) ENGINE = InnoDB COMMENT = '公司主体';

-- 店铺（平台 + 店铺名 + 启用/停用）
CREATE TABLE biz_shop (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  name          VARCHAR(100) NOT NULL COMMENT '店铺名称',
  platform_code VARCHAR(30)  NOT NULL COMMENT '所属平台（字典 platform）',
  status        INT          NOT NULL DEFAULT 1 COMMENT '1启用 0停用（停用后不可录入，历史保留）',
  remark        VARCHAR(500) NULL,
  created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_name (name),
  KEY idx_platform (platform_code),
  KEY idx_status (status)
) ENGINE = InnoDB COMMENT = '店铺';

-- 店铺-负责人版本表（独立版本化：同一店铺同一生效日仅一条）
CREATE TABLE biz_shop_owner_version (
  id             BIGINT AUTO_INCREMENT PRIMARY KEY,
  shop_id        BIGINT NOT NULL,
  owner_id       BIGINT NOT NULL,
  effective_date DATE   NOT NULL COMMENT '生效日期（含当天）',
  created_by     BIGINT NOT NULL DEFAULT 1,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uk_shop_date (shop_id, effective_date),
  KEY idx_owner (owner_id),
  KEY idx_shop_date (shop_id, effective_date),
  CONSTRAINT fk_sov_shop  FOREIGN KEY (shop_id)  REFERENCES biz_shop (id),
  CONSTRAINT fk_sov_owner FOREIGN KEY (owner_id) REFERENCES biz_owner (id)
) ENGINE = InnoDB COMMENT = '店铺负责人变更版本';

-- 店铺-公司主体版本表（与负责人版本相互独立）
CREATE TABLE biz_shop_company_version (
  id             BIGINT AUTO_INCREMENT PRIMARY KEY,
  shop_id        BIGINT NOT NULL,
  company_id     BIGINT NOT NULL,
  effective_date DATE   NOT NULL COMMENT '生效日期（含当天）',
  created_by     BIGINT NOT NULL DEFAULT 1,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uk_shop_date (shop_id, effective_date),
  KEY idx_company (company_id),
  KEY idx_shop_date (shop_id, effective_date),
  CONSTRAINT fk_scv_shop    FOREIGN KEY (shop_id)    REFERENCES biz_shop (id),
  CONSTRAINT fk_scv_company FOREIGN KEY (company_id) REFERENCES biz_company (id)
) ENGINE = InnoDB COMMENT = '店铺公司主体变更版本';

-- 销售登记表（同店同日一条，重复保存即覆盖编辑）
CREATE TABLE biz_sale_record (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  biz_date      DATE          NOT NULL COMMENT '销售日期',
  shop_id       BIGINT        NOT NULL,
  owner_id      BIGINT        NOT NULL COMMENT '录入时负责人快照（历史归属统计依据）',
  company_id    BIGINT        NOT NULL COMMENT '录入时公司主体快照',
  sale_amount   DECIMAL(12,2) NOT NULL DEFAULT 0 COMMENT '销售额（>=0）',
  refund_amount DECIMAL(12,2) NOT NULL DEFAULT 0 COMMENT '退款金额（>=0）',
  remark        VARCHAR(500)  NULL,
  created_by    BIGINT        NOT NULL,
  updated_by    BIGINT        NULL,
  created_at    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_date_shop (biz_date, shop_id),
  KEY idx_owner_date   (owner_id,   biz_date),
  KEY idx_company_date (company_id, biz_date),
  KEY idx_shop_date    (shop_id,    biz_date),
  CONSTRAINT fk_sr_shop   FOREIGN KEY (shop_id)   REFERENCES biz_shop (id),
  CONSTRAINT fk_sr_owner  FOREIGN KEY (owner_id)  REFERENCES biz_owner (id),
  CONSTRAINT fk_sr_comp   FOREIGN KEY (company_id) REFERENCES biz_company (id)
) ENGINE = InnoDB COMMENT = '销售登记表';

-- 补单登记表（按订单逐条登记，同店同日可多条）
CREATE TABLE biz_supplement_record (
  id          BIGINT AUTO_INCREMENT PRIMARY KEY,
  biz_date    DATE          NOT NULL COMMENT '补单日期',
  shop_id     BIGINT        NOT NULL,
  owner_id    BIGINT        NOT NULL COMMENT '录入时负责人快照',
  company_id  BIGINT        NOT NULL COMMENT '录入时公司主体快照',
  order_no    VARCHAR(64)   NULL COMMENT '订单号（各平台格式不一，文本存储）',
  orderer_id  BIGINT        NULL COMMENT '下单人（sys_orderer）',
  amount      DECIMAL(12,2) NOT NULL DEFAULT 0 COMMENT '补单金额（>=0）',
  refunded    INT           NOT NULL DEFAULT 0 COMMENT '是否返款 0否 1是',
  refunded_at DATETIME      NULL COMMENT '返款标记时间（留痕）',
  refunded_by BIGINT        NULL COMMENT '返款操作人（留痕）',
  reason_code VARCHAR(30)   NULL COMMENT '补单原因（字典 supplement_reason）',
  remark      VARCHAR(500)  NULL,
  created_by  BIGINT        NOT NULL,
  updated_by  BIGINT        NULL,
  created_at  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_shop_date (shop_id, biz_date),
  KEY idx_owner_date   (owner_id,   biz_date),
  KEY idx_company_date (company_id, biz_date),
  KEY idx_orderer (orderer_id),
  KEY idx_refunded (refunded),
  KEY idx_date (biz_date),
  CONSTRAINT fk_sup_shop    FOREIGN KEY (shop_id)    REFERENCES biz_shop (id),
  CONSTRAINT fk_sup_owner   FOREIGN KEY (owner_id)   REFERENCES biz_owner (id),
  CONSTRAINT fk_sup_comp    FOREIGN KEY (company_id) REFERENCES biz_company (id),
  CONSTRAINT fk_sup_orderer FOREIGN KEY (orderer_id) REFERENCES sys_orderer (id)
) ENGINE = InnoDB COMMENT = '补单登记表';

-- 补单订单截图附件（一张补单可挂多张图）
CREATE TABLE biz_supplement_attachment (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  supplement_id BIGINT       NOT NULL,
  file_name     VARCHAR(255) NOT NULL COMMENT '原始文件名',
  file_path     VARCHAR(500) NOT NULL COMMENT '服务器相对路径',
  file_size     INT          NOT NULL DEFAULT 0 COMMENT '字节数',
  mime          VARCHAR(100) NOT NULL DEFAULT '',
  uploaded_by   BIGINT       NOT NULL,
  created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_sup (supplement_id),
  CONSTRAINT fk_supa_sup FOREIGN KEY (supplement_id) REFERENCES biz_supplement_record (id) ON DELETE CASCADE
) ENGINE = InnoDB COMMENT = '补单订单截图附件';

-- 推广费登记表（同店同日同推广类型一条）
CREATE TABLE biz_promotion_record (
  id              BIGINT AUTO_INCREMENT PRIMARY KEY,
  biz_date        DATE          NOT NULL COMMENT '投放日期',
  shop_id         BIGINT        NOT NULL,
  owner_id        BIGINT        NOT NULL COMMENT '录入时负责人快照',
  company_id      BIGINT        NOT NULL COMMENT '录入时公司主体快照',
  promo_type_code VARCHAR(30)   NOT NULL COMMENT '推广类型（字典 promo_type）',
  amount          DECIMAL(12,2) NOT NULL DEFAULT 0 COMMENT '推广费（>=0）',
  remark          VARCHAR(500)  NULL,
  created_by      BIGINT        NOT NULL,
  updated_by      BIGINT        NULL,
  created_at      DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at      DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_date_shop_type (biz_date, shop_id, promo_type_code),
  KEY idx_owner_date   (owner_id,   biz_date),
  KEY idx_company_date (company_id, biz_date),
  KEY idx_shop_date    (shop_id,    biz_date),
  CONSTRAINT fk_pr_shop  FOREIGN KEY (shop_id)  REFERENCES biz_shop (id),
  CONSTRAINT fk_pr_owner FOREIGN KEY (owner_id) REFERENCES biz_owner (id),
  CONSTRAINT fk_pr_comp  FOREIGN KEY (company_id) REFERENCES biz_company (id)
) ENGINE = InnoDB COMMENT = '推广费登记表';
