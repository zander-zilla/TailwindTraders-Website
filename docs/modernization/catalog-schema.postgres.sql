CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE TABLE brands (
        id integer NOT NULL,
        name character varying(255) NOT NULL,
        CONSTRAINT pk_brands PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE TABLE tags (
        id integer NOT NULL,
        value character varying(255) NOT NULL,
        CONSTRAINT pk_tags PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE TABLE types (
        id integer NOT NULL,
        code character varying(255) NOT NULL,
        name character varying(255) NOT NULL,
        CONSTRAINT pk_types PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE TABLE products (
        id integer NOT NULL,
        name character varying(255) NOT NULL,
        price numeric(9,2) NOT NULL,
        image_name character varying(255),
        brand_id integer NOT NULL,
        type_id integer NOT NULL,
        tag_id integer,
        CONSTRAINT pk_products PRIMARY KEY (id),
        CONSTRAINT fk_products_brands_brand_id FOREIGN KEY (brand_id) REFERENCES brands (id) ON DELETE RESTRICT,
        CONSTRAINT fk_products_tags_tag_id FOREIGN KEY (tag_id) REFERENCES tags (id) ON DELETE SET NULL,
        CONSTRAINT fk_products_types_type_id FOREIGN KEY (type_id) REFERENCES types (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE TABLE features (
        id integer NOT NULL,
        title character varying(255),
        description text,
        product_item_id integer NOT NULL,
        CONSTRAINT pk_features PRIMARY KEY (id),
        CONSTRAINT fk_features_products_product_item_id FOREIGN KEY (product_item_id) REFERENCES products (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE INDEX ix_features_product_item_id ON features (product_item_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE INDEX ix_products_brand_id ON products (brand_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE INDEX ix_products_tag_id ON products (tag_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE INDEX ix_products_type_id ON products (type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    CREATE UNIQUE INDEX ix_types_code ON types (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261011024659_InitialCatalog') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261011024659_InitialCatalog', '10.0.12');
    END IF;
END $EF$;
COMMIT;

