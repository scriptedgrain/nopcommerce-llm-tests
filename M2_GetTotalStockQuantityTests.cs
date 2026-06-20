using System;
using System.Collections.Generic;
using NUnit.Framework;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;

namespace Nop.Services.Tests.Catalog
{
    [TestFixture]
    public class ProductExtensions_GetTotalStockQuantityTests
    {
        private Product _product;

        [SetUp]
        public void SetUp()
        {
            _product = new Product
            {
                ManageInventoryMethodId = (int)ManageInventoryMethod.ManageStock,
                UseMultipleWarehouses = false,
                StockQuantity = 0
            };
        }

        // ── Condição de erro ────────────────────────────────────────────────

        [Test]
        public void GetTotalStockQuantity_ProductIsNull_ThrowsArgumentNullException()
        {
            Product nullProduct = null;
            Assert.That(() => nullProduct.GetTotalStockQuantity(),
                Throws.TypeOf<ArgumentNullException>()
                      .With.Property("ParamName").EqualTo("product"));
        }

        // ── ManageInventoryMethod != ManageStock ─────────────────────────────

        [Test]
        public void GetTotalStockQuantity_DontManageStock_ReturnsZero()
        {
            _product.ManageInventoryMethodId = (int)ManageInventoryMethod.DontManageStock;
            _product.StockQuantity = 100;

            var result = _product.GetTotalStockQuantity();

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void GetTotalStockQuantity_ManageStockByAttributes_ReturnsZero()
        {
            _product.ManageInventoryMethodId = (int)ManageInventoryMethod.ManageStockByAttributes;
            _product.StockQuantity = 50;

            var result = _product.GetTotalStockQuantity();

            Assert.That(result, Is.EqualTo(0));
        }

        // ── Single warehouse (UseMultipleWarehouses = false) ─────────────────

        [Test]
        public void GetTotalStockQuantity_SingleWarehouse_ReturnsStockQuantity()
        {
            _product.StockQuantity = 42;

            var result = _product.GetTotalStockQuantity();

            Assert.That(result, Is.EqualTo(42));
        }

        [Test]
        public void GetTotalStockQuantity_SingleWarehouse_StockQuantityZero_ReturnsZero()
        {
            _product.StockQuantity = 0;

            var result = _product.GetTotalStockQuantity();

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void GetTotalStockQuantity_SingleWarehouse_NegativeStock_ReturnsNegative()
        {
            _product.StockQuantity = -5;

            var result = _product.GetTotalStockQuantity();

            Assert.That(result, Is.EqualTo(-5));
        }

        // ── Multiple warehouses, sem filtro por warehouseId ──────────────────

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_NoFilter_SumsAllStockQuantities()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 10, ReservedQuantity = 0 });
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 2, StockQuantity = 20, ReservedQuantity = 0 });

            var result = _product.GetTotalStockQuantity(useReservedQuantity: false);

            Assert.That(result, Is.EqualTo(30));
        }

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_NoFilter_UseReservedQuantity_SubtractsReserved()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 10, ReservedQuantity = 3 });
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 2, StockQuantity = 20, ReservedQuantity = 5 });

            // useReservedQuantity defaults to true
            var result = _product.GetTotalStockQuantity();

            Assert.That(result, Is.EqualTo(22)); // (10+20) - (3+5)
        }

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_NoFilter_UseReservedQuantityFalse_DoesNotSubtract()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 10, ReservedQuantity = 3 });
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 2, StockQuantity = 20, ReservedQuantity = 5 });

            var result = _product.GetTotalStockQuantity(useReservedQuantity: false);

            Assert.That(result, Is.EqualTo(30));
        }

        // ── Multiple warehouses, COM filtro por warehouseId ─────────────────

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_FilterByWarehouseId_ReturnsThatWarehouseOnly()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 10, ReservedQuantity = 2 });
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 2, StockQuantity = 40, ReservedQuantity = 10 });

            var result = _product.GetTotalStockQuantity(useReservedQuantity: true, warehouseId: 1);

            Assert.That(result, Is.EqualTo(8)); // 10 - 2
        }

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_FilterByWarehouseId_WarehouseNotFound_ReturnsZero()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 10, ReservedQuantity = 2 });

            var result = _product.GetTotalStockQuantity(useReservedQuantity: true, warehouseId: 99);

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_FilterByWarehouseId_UseReservedFalse_ReturnsRawStock()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 15, ReservedQuantity = 7 });
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 2, StockQuantity = 99, ReservedQuantity = 50 });

            var result = _product.GetTotalStockQuantity(useReservedQuantity: false, warehouseId: 1);

            Assert.That(result, Is.EqualTo(15));
        }

        // ── Casos de borda: lista de inventário vazia ────────────────────────

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_EmptyInventoryList_ReturnsZero()
        {
            _product.UseMultipleWarehouses = true;
            // ProductWarehouseInventory já é inicializado como lista vazia

            var result = _product.GetTotalStockQuantity();

            Assert.That(result, Is.EqualTo(0));
        }

        // ── Reserved maior que stock (resultado negativo) ────────────────────

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_ReservedExceedsStock_ReturnsNegative()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 5, ReservedQuantity = 10 });

            var result = _product.GetTotalStockQuantity(useReservedQuantity: true);

            Assert.That(result, Is.EqualTo(-5));
        }

        // ── warehouseId = 0 equivale a sem filtro ────────────────────────────

        [Test]
        public void GetTotalStockQuantity_MultipleWarehouses_WarehouseIdZero_SumsAllWarehouses()
        {
            _product.UseMultipleWarehouses = true;
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 1, StockQuantity = 10, ReservedQuantity = 1 });
            _product.ProductWarehouseInventory.Add(new ProductWarehouseInventory { WarehouseId = 2, StockQuantity = 20, ReservedQuantity = 2 });

            var result = _product.GetTotalStockQuantity(useReservedQuantity: true, warehouseId: 0);

            Assert.That(result, Is.EqualTo(27)); // (10+20) - (1+2)
        }
    }
}