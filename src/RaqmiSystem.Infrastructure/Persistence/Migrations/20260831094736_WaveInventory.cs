using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.EnsureSchema(
                name: "purchasing");

            migrationBuilder.EnsureSchema(
                name: "kitchen");

            migrationBuilder.CreateTable(
                name: "recipe_sheets",
                schema: "kitchen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    yield_portions = table.Column<int>(type: "integer", nullable: false),
                    allergens = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_sheets", x => x.id);
                    table.CheckConstraint("ck_recipe_sheets_category", "category IN ('Entree', 'Plat', 'Dessert', 'Boisson', 'SousPreparation')");
                    table.CheckConstraint("ck_recipe_sheets_yield_portions_positive", "yield_portions >= 1");
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    minimum_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_items", x => x.id);
                    table.UniqueConstraint("AK_stock_items_code", x => x.code);
                    table.CheckConstraint("ck_stock_items_category", "category IN ('Alimentaire', 'Boisson', 'Entretien', 'Equipement', 'Autre')");
                    table.CheckConstraint("ck_stock_items_minimum_quantity_non_negative", "CAST(minimum_quantity AS numeric) >= 0");
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nif = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    rc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    nis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                    table.UniqueConstraint("AK_suppliers_code", x => x.code);
                    table.CheckConstraint("ck_suppliers_nif_length", "nif IS NULL OR length(nif) = 15");
                    table.CheckConstraint("ck_suppliers_supplier_type", "supplier_type IN ('Company', 'Individual', 'PublicEntity')");
                });

            migrationBuilder.CreateTable(
                name: "temperature_checkpoints",
                schema: "kitchen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    min_temp = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    max_temp = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temperature_checkpoints", x => x.id);
                    table.UniqueConstraint("AK_temperature_checkpoints_code", x => x.code);
                    table.CheckConstraint("ck_temperature_checkpoints_range", "CAST(min_temp AS numeric) < CAST(max_temp AS numeric)");
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                    table.UniqueConstraint("AK_warehouses_code", x => x.code);
                    table.ForeignKey(
                        name: "FK_warehouses_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredients",
                schema: "kitchen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_sheet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_ingredients", x => x.id);
                    table.CheckConstraint("ck_recipe_ingredients_line_number_positive", "line_number >= 1");
                    table.CheckConstraint("ck_recipe_ingredients_quantity_positive", "CAST(quantity AS numeric) > 0");
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_recipe_sheets_recipe_sheet_id",
                        column: x => x.recipe_sheet_id,
                        principalSchema: "kitchen",
                        principalTable: "recipe_sheets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    approved_year = table.Column<int>(type: "integer", nullable: true),
                    approved_sequence = table.Column<int>(type: "integer", nullable: true),
                    supplier_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    warehouse_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_excl_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.CheckConstraint("ck_purchase_orders_status", "status IN ('Draft', 'Approved', 'PartiallyReceived', 'Received', 'Cancelled')");
                    table.CheckConstraint("ck_purchase_orders_total_positive", "CAST(total_excl_vat AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_supplier_code",
                        column: x => x.supplier_code,
                        principalSchema: "purchasing",
                        principalTable: "suppliers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "temperature_readings",
                schema: "kitchen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkpoint_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    measured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    value_celsius = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    recorded_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    min_temp_snapshot = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    max_temp_snapshot = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    is_compliant = table.Column<bool>(type: "boolean", nullable: false),
                    corrective_action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temperature_readings", x => x.id);
                    table.CheckConstraint("ck_temperature_readings_corrective_action", "is_compliant OR corrective_action IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_temperature_readings_temperature_checkpoints_checkpoint_code",
                        column: x => x.checkpoint_code,
                        principalSchema: "kitchen",
                        principalTable: "temperature_checkpoints",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_counts",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    count_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_counts", x => x.id);
                    table.CheckConstraint("ck_inventory_counts_status", "status IN ('Draft', 'Validated')");
                    table.ForeignKey(
                        name: "FK_inventory_counts_warehouses_warehouse_code",
                        column: x => x.warehouse_code,
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    item_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    movement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    lot_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    adjustment_is_increase = table.Column<bool>(type: "boolean", nullable: true),
                    transfer_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_adjustment_direction", "(kind = 'InventoryAdjustment' AND adjustment_is_increase IS NOT NULL) OR (kind <> 'InventoryAdjustment' AND adjustment_is_increase IS NULL)");
                    table.CheckConstraint("ck_stock_movements_kind", "kind IN ('PurchaseEntry', 'Consumption', 'TransferOut', 'TransferIn', 'InventoryAdjustment')");
                    table.CheckConstraint("ck_stock_movements_purchase_entry_costed", "kind <> 'PurchaseEntry' OR unit_cost IS NOT NULL");
                    table.CheckConstraint("ck_stock_movements_quantity_positive", "CAST(quantity AS numeric) > 0");
                    table.CheckConstraint("ck_stock_movements_unit_cost_non_negative", "unit_cost IS NULL OR CAST(unit_cost AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_stock_movements_stock_items_item_code",
                        column: x => x.item_code,
                        principalSchema: "inventory",
                        principalTable: "stock_items",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_warehouses_warehouse_code",
                        column: x => x.warehouse_code,
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    designation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_total_excl_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity_received = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.id);
                    table.CheckConstraint("ck_purchase_order_lines_line_number_positive", "line_number >= 1");
                    table.CheckConstraint("ck_purchase_order_lines_quantities", "CAST(quantity AS numeric) > 0 AND CAST(unit_price AS numeric) >= 0 AND CAST(quantity_received AS numeric) >= 0 AND CAST(quantity_received AS numeric) <= CAST(quantity AS numeric)");
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "purchasing",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_count_lines",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_count_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    counted_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_count_lines", x => x.id);
                    table.CheckConstraint("ck_inventory_count_lines_counted_quantity_non_negative", "CAST(counted_quantity AS numeric) >= 0");
                    table.CheckConstraint("ck_inventory_count_lines_line_number_positive", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_inventory_counts_inventory_count_id",
                        column: x => x.inventory_count_id,
                        principalSchema: "inventory",
                        principalTable: "inventory_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_stock_items_item_code",
                        column: x => x.item_code,
                        principalSchema: "inventory",
                        principalTable: "stock_items",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_item_code",
                schema: "inventory",
                table: "inventory_count_lines",
                column: "item_code");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_count_lines_count_item",
                schema: "inventory",
                table: "inventory_count_lines",
                columns: new[] { "inventory_count_id", "item_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_count_date",
                schema: "inventory",
                table: "inventory_counts",
                column: "count_date");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_status",
                schema: "inventory",
                table: "inventory_counts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_warehouse_code",
                schema: "inventory",
                table: "inventory_counts",
                column: "warehouse_code");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_item_code",
                schema: "purchasing",
                table: "purchase_order_lines",
                column: "item_code");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_purchase_order_id",
                schema: "purchasing",
                table: "purchase_order_lines",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_order_date",
                schema: "purchasing",
                table: "purchase_orders",
                column: "order_date");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_status",
                schema: "purchasing",
                table: "purchase_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_supplier_code",
                schema: "purchasing",
                table: "purchase_orders",
                column: "supplier_code");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_warehouse_code",
                schema: "purchasing",
                table: "purchase_orders",
                column: "warehouse_code");

            migrationBuilder.CreateIndex(
                name: "ux_purchase_orders_approved_year_sequence",
                schema: "purchasing",
                table: "purchase_orders",
                columns: new[] { "approved_year", "approved_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_purchase_orders_number",
                schema: "purchasing",
                table: "purchase_orders",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_recipe_sheet_id",
                schema: "kitchen",
                table: "recipe_ingredients",
                column: "recipe_sheet_id");

            migrationBuilder.CreateIndex(
                name: "ux_recipe_ingredients_recipe_item",
                schema: "kitchen",
                table: "recipe_ingredients",
                columns: new[] { "recipe_sheet_id", "item_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipe_sheets_category",
                schema: "kitchen",
                table: "recipe_sheets",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ux_recipe_sheets_code",
                schema: "kitchen",
                table: "recipe_sheets",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_category",
                schema: "inventory",
                table: "stock_items",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ux_stock_items_code",
                schema: "inventory",
                table: "stock_items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_item_code",
                schema: "inventory",
                table: "stock_movements",
                column: "item_code");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_kind",
                schema: "inventory",
                table: "stock_movements",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_movement_date",
                schema: "inventory",
                table: "stock_movements",
                column: "movement_date");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_transfer_group_id",
                schema: "inventory",
                table: "stock_movements",
                column: "transfer_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_warehouse_item",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "warehouse_code", "item_code" });

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_is_active",
                schema: "purchasing",
                table: "suppliers",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_name",
                schema: "purchasing",
                table: "suppliers",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ux_temperature_checkpoints_code",
                schema: "kitchen",
                table: "temperature_checkpoints",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_temperature_readings_checkpoint_code",
                schema: "kitchen",
                table: "temperature_readings",
                column: "checkpoint_code");

            migrationBuilder.CreateIndex(
                name: "ix_temperature_readings_is_compliant",
                schema: "kitchen",
                table: "temperature_readings",
                column: "is_compliant");

            migrationBuilder.CreateIndex(
                name: "ix_temperature_readings_measured_at",
                schema: "kitchen",
                table: "temperature_readings",
                column: "measured_at");

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_hotel_unit_code",
                schema: "inventory",
                table: "warehouses",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ux_warehouses_code",
                schema: "inventory",
                table: "warehouses",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_count_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "recipe_ingredients",
                schema: "kitchen");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "temperature_readings",
                schema: "kitchen");

            migrationBuilder.DropTable(
                name: "inventory_counts",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "recipe_sheets",
                schema: "kitchen");

            migrationBuilder.DropTable(
                name: "stock_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "temperature_checkpoints",
                schema: "kitchen");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "purchasing");
        }
    }
}
