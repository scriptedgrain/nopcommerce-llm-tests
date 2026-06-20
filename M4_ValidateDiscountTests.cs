using Moq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Data;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Core.Plugins;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Discounts.Cache;
using Nop.Services.Events;
using Nop.Services.Localization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using static Nop.Services.Discounts.DiscountService;

namespace Nop.Services.Tests.Discounts
{
    [TestFixture]
    public class DiscountServiceValidateDiscountTests
    {
        private Mock<ICacheManager> _cacheManagerMock;
        private Mock<IRepository<Discount>> _discountRepositoryMock;
        private Mock<IRepository<DiscountRequirement>> _discountRequirementRepositoryMock;
        private Mock<IRepository<DiscountUsageHistory>> _discountUsageHistoryRepositoryMock;
        private Mock<IStoreContext> _storeContextMock;
        private Mock<ILocalizationService> _localizationServiceMock;
        private Mock<ICategoryService> _categoryServiceMock;
        private Mock<IPluginFinder> _pluginFinderMock;
        private Mock<IEventPublisher> _eventPublisherMock;
        private Mock<IWorkContext> _workContextMock;

        private DiscountService _discountService;
        private Store _currentStore;

        [SetUp]
        public void SetUp()
        {
            _cacheManagerMock = new Mock<ICacheManager>();
            _discountRepositoryMock = new Mock<IRepository<Discount>>();
            _discountRequirementRepositoryMock = new Mock<IRepository<DiscountRequirement>>();
            _discountUsageHistoryRepositoryMock = new Mock<IRepository<DiscountUsageHistory>>();
            _storeContextMock = new Mock<IStoreContext>();
            _localizationServiceMock = new Mock<ILocalizationService>();
            _categoryServiceMock = new Mock<ICategoryService>();
            _pluginFinderMock = new Mock<IPluginFinder>();
            _eventPublisherMock = new Mock<IEventPublisher>();
            _workContextMock = new Mock<IWorkContext>();

            _currentStore = new Store { Id = 1, Name = "Test Store" };
            _storeContextMock.Setup(s => s.CurrentStore).Returns(_currentStore);

            _localizationServiceMock
                .Setup(l => l.GetResource(It.IsAny<string>()))
                .Returns<string>(key => key);

            // Default cache manager: execute factory and return result
            _cacheManagerMock
                .Setup(c => c.Get(It.IsAny<string>(), It.IsAny<Func<IList<DiscountService.DiscountRequirementForCaching>>>()))
                .Returns<string, Func<IList<DiscountService.DiscountRequirementForCaching>>>((key, factory) => factory());

            // Default requirement repository returns empty
            var emptyRequirements = new List<DiscountRequirement>().AsQueryable();
            _discountRequirementRepositoryMock.Setup(r => r.Table).Returns(emptyRequirements);

            // Default usage history repository returns empty
            var emptyHistory = new List<DiscountUsageHistory>().AsQueryable();
            _discountUsageHistoryRepositoryMock.Setup(r => r.Table).Returns(emptyHistory);

            _discountService = new DiscountService(
                _cacheManagerMock.Object,
                _discountRepositoryMock.Object,
                _discountRequirementRepositoryMock.Object,
                _discountUsageHistoryRepositoryMock.Object,
                _storeContextMock.Object,
                _localizationServiceMock.Object,
                _categoryServiceMock.Object,
                _pluginFinderMock.Object,
                _eventPublisherMock.Object,
                _workContextMock.Object
            );
        }

        #region Helpers

        private Customer CreateRegisteredCustomer(int id = 1)
        {
            var role = new CustomerRole { Active = true, SystemName = SystemCustomerRoleNames.Registered };
            var customer = new Customer { Id = id, Active = true, Email = "test@test.com" };
            customer.CustomerRoles.Add(role);
            return customer;
        }

        private Customer CreateGuestCustomer(int id = 99)
        {
            var role = new CustomerRole { Active = true, SystemName = SystemCustomerRoleNames.Guests };
            var customer = new Customer { Id = id, Active = true };
            customer.CustomerRoles.Add(role);
            return customer;
        }

        private DiscountForCaching CreateBasicDiscount(
            int id = 1,
            DiscountType discountType = DiscountType.AssignedToSkus,
            DiscountLimitationType limitation = DiscountLimitationType.Unlimited,
            bool requiresCoupon = false,
            string couponCode = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int limitationTimes = 0)
        {
            return new DiscountForCaching
            {
                Id = id,
                DiscountType = discountType,
                DiscountLimitation = limitation,
                RequiresCouponCode = requiresCoupon,
                CouponCode = couponCode,
                StartDateUtc = startDate,
                EndDateUtc = endDate,
                LimitationTimes = limitationTimes
            };
        }

        #endregion

        #region Null argument guards

        [Test]
        public void ValidateDiscount_DiscountForCaching_NullDiscount_ThrowsArgumentNullException()
        {
            var customer = CreateRegisteredCustomer();
            Assert.Throws<ArgumentNullException>(() =>
                _discountService.ValidateDiscount((DiscountForCaching)null, customer));
        }

        [Test]
        public void ValidateDiscount_DiscountForCaching_NullCustomer_ThrowsArgumentNullException()
        {
            var discount = CreateBasicDiscount();
            Assert.Throws<ArgumentNullException>(() =>
                _discountService.ValidateDiscount(discount, (Customer)null));
        }

        [Test]
        public void ValidateDiscount_Discount_NullDiscount_ThrowsArgumentNullException()
        {
            var customer = CreateRegisteredCustomer();
            Assert.Throws<ArgumentNullException>(() =>
                _discountService.ValidateDiscount((Discount)null, customer));
        }

        #endregion

        #region Happy path — no requirements, no coupon, no date restrictions, unlimited

        [Test]
        public void ValidateDiscount_NoRequirements_Unlimited_ReturnsValid()
        {
            var discount = CreateBasicDiscount();
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Coupon code validation

        [Test]
        public void ValidateDiscount_RequiresCoupon_NullCouponCodes_ReturnsInvalid()
        {
            var discount = CreateBasicDiscount(requiresCoupon: true, couponCode: "SAVE10");
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, null);

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidateDiscount_RequiresCoupon_EmptyCouponCodeOnDiscount_ReturnsInvalid()
        {
            var discount = CreateBasicDiscount(requiresCoupon: true, couponCode: "");
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new[] { "SAVE10" });

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidateDiscount_RequiresCoupon_WrongCode_ReturnsInvalid()
        {
            var discount = CreateBasicDiscount(requiresCoupon: true, couponCode: "SAVE10");
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new[] { "WRONGCODE" });

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidateDiscount_RequiresCoupon_CorrectCode_ReturnsValid()
        {
            var discount = CreateBasicDiscount(requiresCoupon: true, couponCode: "SAVE10");
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new[] { "SAVE10" });

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDiscount_RequiresCoupon_CodeIsCaseInsensitive_ReturnsValid()
        {
            var discount = CreateBasicDiscount(requiresCoupon: true, couponCode: "save10");
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new[] { "SAVE10" });

            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Date range validation

        [Test]
        public void ValidateDiscount_StartDateInFuture_ReturnsInvalidWithError()
        {
            var discount = CreateBasicDiscount(startDate: DateTime.UtcNow.AddDays(1));
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors, Contains.Item("ShoppingCart.Discount.NotStartedYet"));
        }

        [Test]
        public void ValidateDiscount_StartDateInPast_ReturnsValid()
        {
            var discount = CreateBasicDiscount(startDate: DateTime.UtcNow.AddDays(-1));
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDiscount_EndDateInPast_ReturnsInvalidWithError()
        {
            var discount = CreateBasicDiscount(endDate: DateTime.UtcNow.AddDays(-1));
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Contains.Item("ShoppingCart.Discount.Expired"));
        }

        [Test]
        public void ValidateDiscount_EndDateInFuture_ReturnsValid()
        {
            var discount = CreateBasicDiscount(endDate: DateTime.UtcNow.AddDays(1));
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Discount limitation — NTimesOnly

        [Test]
        public void ValidateDiscount_NTimesOnly_UsedTimesLessThanLimit_ReturnsValid()
        {
            var history = new List<DiscountUsageHistory>
            {
                new DiscountUsageHistory { DiscountId = 1, CreatedOnUtc = DateTime.UtcNow }
            }.AsQueryable();

            _discountUsageHistoryRepositoryMock.Setup(r => r.Table).Returns(history);

            var discount = CreateBasicDiscount(
                limitation: DiscountLimitationType.NTimesOnly,
                limitationTimes: 5);

            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDiscount_NTimesOnly_UsedTimesEqualLimit_ReturnsInvalid()
        {
            var history = new List<DiscountUsageHistory>
            {
                new DiscountUsageHistory { DiscountId = 1, CreatedOnUtc = DateTime.UtcNow },
                new DiscountUsageHistory { DiscountId = 1, CreatedOnUtc = DateTime.UtcNow }
            }.AsQueryable();

            _discountUsageHistoryRepositoryMock.Setup(r => r.Table).Returns(history);

            var discount = CreateBasicDiscount(
                limitation: DiscountLimitationType.NTimesOnly,
                limitationTimes: 2);

            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidateDiscount_NTimesOnly_UsedTimesExceedLimit_ReturnsInvalid()
        {
            var history = Enumerable.Range(0, 5).Select(i => new DiscountUsageHistory
            {
                DiscountId = 1,
                CreatedOnUtc = DateTime.UtcNow
            }).AsQueryable();

            _discountUsageHistoryRepositoryMock.Setup(r => r.Table).Returns(history);

            var discount = CreateBasicDiscount(
                limitation: DiscountLimitationType.NTimesOnly,
                limitationTimes: 3);

            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.False);
        }

        #endregion

        #region Discount limitation — NTimesPerCustomer

        [Test]
        public void ValidateDiscount_NTimesPerCustomer_RegisteredCustomer_WithinLimit_ReturnsValid()
        {
            var customer = CreateRegisteredCustomer(id: 42);

            var history = new List<DiscountUsageHistory>().AsQueryable();
            _discountUsageHistoryRepositoryMock.Setup(r => r.Table).Returns(history);

            var discount = CreateBasicDiscount(
                limitation: DiscountLimitationType.NTimesPerCustomer,
                limitationTimes: 2);

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDiscount_NTimesPerCustomer_RegisteredCustomer_AtLimit_ReturnsInvalidWithError()
        {
            var customer = CreateRegisteredCustomer(id: 42);

            var order = new Order { CustomerId = 42 };
            var history = new List<DiscountUsageHistory>
            {
                new DiscountUsageHistory { DiscountId = 1, Order = order, CreatedOnUtc = DateTime.UtcNow }
            }.AsQueryable();

            _discountUsageHistoryRepositoryMock.Setup(r => r.Table).Returns(history);

            var discount = CreateBasicDiscount(
                limitation: DiscountLimitationType.NTimesPerCustomer,
                limitationTimes: 1);

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Contains.Item("ShoppingCart.Discount.CannotBeUsedAnymore"));
        }

        [Test]
        public void ValidateDiscount_NTimesPerCustomer_GuestCustomer_ReturnsValid()
        {
            // Guests are not registered; limitation per customer is skipped
            var customer = CreateGuestCustomer();

            var discount = CreateBasicDiscount(
                limitation: DiscountLimitationType.NTimesPerCustomer,
                limitationTimes: 0);

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Gift card restriction

        [Test]
        public void ValidateDiscount_AssignedToOrderSubTotal_CartHasGiftCard_ReturnsInvalidWithError()
        {
            var customer = CreateRegisteredCustomer();
            var giftCardProduct = new Nop.Core.Domain.Catalog.Product { IsGiftCard = true };
            var cartItem = new ShoppingCartItem
            {
                ShoppingCartTypeId = (int)ShoppingCartType.ShoppingCart,
                StoreId = 1,
                Product = giftCardProduct
            };
            customer.ShoppingCartItems.Add(cartItem);

            var discount = CreateBasicDiscount(discountType: DiscountType.AssignedToOrderSubTotal);

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Contains.Item("ShoppingCart.Discount.CannotBeUsedWithGiftCards"));
        }

        [Test]
        public void ValidateDiscount_AssignedToOrderTotal_CartHasGiftCard_ReturnsInvalidWithError()
        {
            var customer = CreateRegisteredCustomer();
            var giftCardProduct = new Nop.Core.Domain.Catalog.Product { IsGiftCard = true };
            var cartItem = new ShoppingCartItem
            {
                ShoppingCartTypeId = (int)ShoppingCartType.ShoppingCart,
                StoreId = 1,
                Product = giftCardProduct
            };
            customer.ShoppingCartItems.Add(cartItem);

            var discount = CreateBasicDiscount(discountType: DiscountType.AssignedToOrderTotal);

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Contains.Item("ShoppingCart.Discount.CannotBeUsedWithGiftCards"));
        }

        [Test]
        public void ValidateDiscount_AssignedToOrderSubTotal_CartHasNoGiftCard_ContinuesValidation()
        {
            var customer = CreateRegisteredCustomer();
            var normalProduct = new Nop.Core.Domain.Catalog.Product { IsGiftCard = false };
            var cartItem = new ShoppingCartItem
            {
                ShoppingCartTypeId = (int)ShoppingCartType.ShoppingCart,
                StoreId = 1,
                Product = normalProduct
            };
            customer.ShoppingCartItems.Add(cartItem);

            var discount = CreateBasicDiscount(discountType: DiscountType.AssignedToOrderSubTotal);

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            // Should pass gift card check and reach requirements check (no requirements → valid)
            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region No requirements (top-level group empty or missing)

        [Test]
        public void ValidateDiscount_NoDiscountRequirements_ReturnsValid()
        {
            var emptyRequirements = new List<DiscountRequirement>().AsQueryable();
            _discountRequirementRepositoryMock.Setup(r => r.Table).Returns(emptyRequirements);

            var discount = CreateBasicDiscount();
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDiscount_TopLevelGroupWithNoChildren_ReturnsValid()
        {
            var topGroup = new DiscountRequirementForCaching
            {
                Id = 1,
                IsGroup = true,
                InteractionType = RequirementGroupInteractionType.And,
                ChildRequirements = new List<DiscountRequirementForCaching>()
            };

            _cacheManagerMock
                .Setup(c => c.Get(It.IsAny<string>(), It.IsAny<Func<IList<DiscountRequirementForCaching>>>()))
                .Returns(new List<DiscountRequirementForCaching> { topGroup });

            var discount = CreateBasicDiscount();
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Combined scenarios

        [Test]
        public void ValidateDiscount_ValidCoupon_AndStartDatePassed_AndUnlimited_ReturnsValid()
        {
            var discount = CreateBasicDiscount(
                requiresCoupon: true,
                couponCode: "PROMO",
                startDate: DateTime.UtcNow.AddDays(-2),
                endDate: DateTime.UtcNow.AddDays(5),
                limitation: DiscountLimitationType.Unlimited);

            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new[] { "PROMO" });

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateDiscount_ValidCoupon_ButExpired_ReturnsInvalid()
        {
            var discount = CreateBasicDiscount(
                requiresCoupon: true,
                couponCode: "EXPIRED",
                endDate: DateTime.UtcNow.AddDays(-1));

            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new[] { "EXPIRED" });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Contains.Item("ShoppingCart.Discount.Expired"));
        }

        #endregion

        #region Result object structure

        [Test]
        public void ValidateDiscount_InvalidResult_HasErrorsList()
        {
            var discount = CreateBasicDiscount(startDate: DateTime.UtcNow.AddDays(10));
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ValidateDiscount_ValidResult_IsValidIsTrue()
        {
            var discount = CreateBasicDiscount();
            var customer = CreateRegisteredCustomer();

            var result = _discountService.ValidateDiscount(discount, customer, new string[0]);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True);
        }

        #endregion
    }
}