CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS organization;
CREATE SCHEMA IF NOT EXISTS exploitation;

CREATE TABLE IF NOT EXISTS organization.hotel_units (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(40) NOT NULL,
    name varchar(160) NOT NULL,
    unit_type varchar(40) NOT NULL DEFAULT 'Hotel',
    display_order integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(160) NOT NULL DEFAULT 'system',
    updated_at timestamptz NULL,
    updated_by varchar(160) NULL
);

ALTER TABLE organization.hotel_units
    ADD COLUMN IF NOT EXISTS unit_type varchar(40) NOT NULL DEFAULT 'Hotel',
    ADD COLUMN IF NOT EXISTS display_order integer NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_hotel_units_display_order_non_negative'
    ) THEN
        ALTER TABLE organization.hotel_units
            ADD CONSTRAINT ck_hotel_units_display_order_non_negative CHECK (display_order >= 0);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_hotel_units_unit_type'
    ) THEN
        ALTER TABLE organization.hotel_units
            ADD CONSTRAINT ck_hotel_units_unit_type CHECK (unit_type IN ('Hotel', 'Residence', 'BeachClub', 'Marina', 'Other'));
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_organization_hotel_units_code
    ON organization.hotel_units (code);

CREATE INDEX IF NOT EXISTS ix_organization_hotel_units_display_order
    ON organization.hotel_units (display_order);

CREATE TABLE IF NOT EXISTS exploitation.daily_revenues (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_date date NOT NULL,
    hotel_unit_code varchar(40) NOT NULL,
    accommodation numeric(18, 2) NOT NULL DEFAULT 0,
    food numeric(18, 2) NOT NULL DEFAULT 0,
    beverage numeric(18, 2) NOT NULL DEFAULT 0,
    other_revenue numeric(18, 2) NOT NULL DEFAULT 0,
    notes varchar(1000) NULL,
    status varchar(30) NOT NULL DEFAULT 'Draft',
    submitted_at timestamptz NULL,
    submitted_by varchar(160) NULL,
    validated_at timestamptz NULL,
    validated_by varchar(160) NULL,
    rejection_reason varchar(500) NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(160) NOT NULL DEFAULT 'system',
    updated_at timestamptz NULL,
    updated_by varchar(160) NULL
);

ALTER TABLE exploitation.daily_revenues
    ADD COLUMN IF NOT EXISTS notes varchar(1000) NULL,
    ADD COLUMN IF NOT EXISTS status varchar(30) NOT NULL DEFAULT 'Draft',
    ADD COLUMN IF NOT EXISTS submitted_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS submitted_by varchar(160) NULL,
    ADD COLUMN IF NOT EXISTS validated_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS validated_by varchar(160) NULL,
    ADD COLUMN IF NOT EXISTS rejection_reason varchar(500) NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_daily_revenues_hotel_unit_code'
    ) THEN
        ALTER TABLE exploitation.daily_revenues
            ADD CONSTRAINT fk_daily_revenues_hotel_unit_code
            FOREIGN KEY (hotel_unit_code)
            REFERENCES organization.hotel_units(code);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_daily_revenues_amounts_non_negative'
    ) THEN
        ALTER TABLE exploitation.daily_revenues
            ADD CONSTRAINT ck_daily_revenues_amounts_non_negative CHECK (
                accommodation >= 0 AND
                food >= 0 AND
                beverage >= 0 AND
                other_revenue >= 0
            );
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_daily_revenues_status'
    ) THEN
        ALTER TABLE exploitation.daily_revenues
            ADD CONSTRAINT ck_daily_revenues_status CHECK (status IN ('Draft', 'Submitted', 'Validated', 'Rejected'));
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_exploitation_daily_revenues_date_unit
    ON exploitation.daily_revenues (business_date, hotel_unit_code);

CREATE INDEX IF NOT EXISTS ix_exploitation_daily_revenues_status
    ON exploitation.daily_revenues (status);

INSERT INTO organization.hotel_units (code, name, unit_type, display_order, is_active, created_by)
VALUES
    ('EL-MANAR', 'Hotel El Manar', 'Hotel', 10, true, 'system'),
    ('EL-MARSA', 'Hotel El Marsa', 'Hotel', 20, true, 'system'),
    ('EL-RIADH', 'Hotel El Riadh', 'Hotel', 30, true, 'system'),
    ('CENTRE-TOURISTIQUE', 'Centre Touristique', 'Residence', 40, true, 'system'),
    ('AZUR-PLAGE', 'Club de Vacances Azur Plage', 'BeachClub', 50, true, 'system'),
    ('PORT-SIDI-FREDJ', 'Port de plaisance Sidi Fredj', 'Marina', 60, true, 'system')
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    unit_type = EXCLUDED.unit_type,
    display_order = EXCLUDED.display_order,
    is_active = EXCLUDED.is_active,
    updated_at = now(),
    updated_by = 'system';
