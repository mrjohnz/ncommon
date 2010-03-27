using System;
using System.Linq;
using NCommon.Data.NHibernate.Tests.HRDomain.Domain;
using NCommon.Data.NHibernate.Tests.OrdersDomain;
using NCommon.Extensions;
using NCommon.Specifications;
using NHibernate;
using NHibernate.Criterion;
using NUnit.Framework;
using Rhino.Mocks;
using Order = NCommon.Data.NHibernate.Tests.OrdersDomain.Order;

namespace NCommon.Data.NHibernate.Tests
{
    /// <summary>
    /// Runs standard query tests on NHRepository
    /// </summary>
    [TestFixture]
    public class NHRepositoryQueryTests : NHRepositoryTestBase
    {
        [Test]
        public void can_perform_simple_query()
        {
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                var customerId = 0;
                testData.Batch(x => customerId = x.CreateCustomer().CustomerID);

                using (var scope = new UnitOfWorkScope())
                {
                    var customer = new NHRepository<Customer>()
                        .Where(x => x.CustomerID == customerId)
                        .First();

                    Assert.That(customer, Is.Not.Null);
                    Assert.That(customer.CustomerID, Is.EqualTo(customerId));
                    scope.Commit();
                }
            }
        }

        [Test]
        public void can_save()
        {
            var customer = new Customer
            {
                FirstName = "Jane",
                LastName = "Doe",
                Address = new Address
                {
                    StreetAddress1 = "123 Main St",
                    City = "Sunset City",
                    State = "LA",
                    ZipCode = "12345"
                }
            };

            using (var scope = new UnitOfWorkScope())
            {
                var repository = new NHRepository<Customer>();
                repository.Save(customer);
                scope.Commit();
            }
            Assert.That(customer.CustomerID, Is.GreaterThan(0));
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Customer savedCustomer = null;
                testData.Batch(action => savedCustomer = action.GetCustomerById(customer.CustomerID));
                Assert.That(savedCustomer, Is.Not.Null);
                Assert.That(savedCustomer.CustomerID, Is.EqualTo(customer.CustomerID));
            }
        }

        [Test]
        public void can_modify()
        {
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Customer customer = null;
                testData.Batch(x => customer = x.CreateCustomer());

                using (var scope = new UnitOfWorkScope())
                {
                    var savedCustomer = new NHRepository<Customer>()
                        .Where(x => x.CustomerID == customer.CustomerID)
                        .First();

                    savedCustomer.LastName = "Changed";
                    scope.Commit();
                }

                testData.Session.Refresh(customer);
                Assert.That(customer.LastName, Is.EqualTo("Changed"));
            }
        }

        [Test]
        public void can_delete()
        {
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
            };
            using (var scope = new UnitOfWorkScope())
            {
                new NHRepository<Customer>().Save(customer);
                scope.Commit();
            }
            Assert.That(customer.CustomerID, Is.GreaterThan(0));
            using (var scope = new UnitOfWorkScope())
            {
                var repository = new NHRepository<Customer>();
                var savedCustomer = repository.Where(x => x.CustomerID == customer.CustomerID).First();
                repository.Delete(savedCustomer);
                scope.Commit();
            }

            //Making sure customer is deleted
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Customer savedCustomer = null;
                testData.Batch(x => savedCustomer = x.GetCustomerById(customer.CustomerID));
                Assert.That(savedCustomer, Is.Null);
            }
        }

        [Test]
        public void can_detach()
        {
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Customer customer = null;
                testData.Batch(action => customer = action.CreateCustomer());

                using (var scope = new UnitOfWorkScope())
                {
                    var repository = new NHRepository<Customer>();
                    var savedCustomer = repository
                        .Where(x => x.CustomerID == customer.CustomerID)
                        .First();

                    repository.Detach(savedCustomer);
                    savedCustomer.LastName = "Changed"; //This shouldn't be saved since the savedCustomer instance is detached.
                    scope.Commit();
                }

                testData.Session.Refresh(customer);
                Assert.That(customer.LastName, Is.Not.EqualTo("Changed"));
            }
        }

        [Test]
        public void can_attach()
        {
            var customer = new Customer
            {
                FirstName = "Jane",
                LastName = "Doe"
            };
            var session = OrdersDomainFactory.OpenSession();
            session.Save(customer);
            session.Evict(customer); //Detching from owning session
            session.Dispose(); //Auto flush

            using (var scope = new UnitOfWorkScope())
            {
                var repository = new NHRepository<Customer>();
                repository.Attach(customer);
                customer.LastName = "Changed";
                scope.Commit(); //Should change since the customer was attached to repository.
            }

            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Customer savedCustomer = null;
                testData.Batch(x => savedCustomer = x.GetCustomerById(customer.CustomerID));
                Assert.That(savedCustomer, Is.Not.Null);
                Assert.That(savedCustomer.LastName, Is.EqualTo("Changed"));
            }
        }

        [Test]
        public void can_query_using_specification()
        {
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                testData.Batch(actions =>
                {
                    actions.CreateOrdersForCustomers(actions.CreateCustomersInState("PA", 2));
                    actions.CreateOrdersForCustomers(actions.CreateCustomersInState("DE", 5));
                    actions.CreateOrdersForCustomers(actions.CreateCustomersInState("LA", 3));
                });

                using (new UnitOfWorkScope())
                {


                    var customersInPA = new Specification<Order>(x => x.Customer.Address.State == "DE");

                    var ordersRepository = new NHRepository<Order>();
                    var results = from order in ordersRepository.Query(customersInPA) select order;

                    Assert.That(results.Count(), Is.GreaterThan(0));
                    Assert.That(results.Count(), Is.EqualTo(5));
                }
            }
        }

        [Test]
        public void can_lazyload()
        {
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Customer customer = null;
                testData.Batch(x =>
                {
                    customer = x.CreateCustomer();
                    x.CreateOrderForCustomer(customer);
                });

                using (var scope = new UnitOfWorkScope())
                {
                    var savedCustomer = new NHRepository<Customer>()
                        .Where(x => x.CustomerID == customer.CustomerID)
                        .First();

                    Assert.That(savedCustomer, Is.Not.Null);
                    Assert.That(savedCustomer.Orders, Is.Not.Null);
                    Assert.That(savedCustomer.Orders.Count, Is.GreaterThan(0));
                    scope.Commit();
                }
            }
        }

        [Test]
        public void lazyloading_when_outside_scope_throws()
        {
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Order order = null;
                testData.Batch(x => order = x.CreateOrderForCustomer(x.CreateCustomer()));

                Order savedOrder = null;
                using (var scope = new UnitOfWorkScope())
                {
                    savedOrder = new NHRepository<Order>()
                        .Where(x => x.OrderID == order.OrderID)
                        .First();
                    scope.Commit();
                }
                Assert.That(savedOrder, Is.Not.Null);
                Assert.That(NHibernateUtil.IsInitialized(savedOrder.Customer), Is.False);
                Assert.Throws<LazyInitializationException>(() => { var firstName = savedOrder.Customer.FirstName; });
            }
        }

        [Test]
        public void can_eager_fetch_using_with()
        {
            using (var tesData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Order order = null;
                tesData.Batch(x => order = x.CreateOrderForCustomer(x.CreateCustomer()));

                Order savedOrder = null;
                using (var scope = new UnitOfWorkScope())
                {
                    savedOrder = new NHRepository<Order>()
                        .With(x => x.Customer)
                        .Where(x => x.OrderID == order.OrderID)
                        .First();
                    scope.Commit();
                }

                Assert.That(savedOrder, Is.Not.Null);
                Assert.That(NHibernateUtil.IsInitialized(savedOrder.Customer));
                Assert.DoesNotThrow(() => { var firstName = savedOrder.Customer.FirstName; });
            }
        }

        public class FakeFetchingStrategy : IFetchingStrategy<Customer, NHRepositoryQueryTests>
        {
            public void Define(IRepository<Customer> repository)
            {
                repository.With(x => x.Orders);
                repository.With<Order>(x => x.Items);
            }
        }

        [Test]
        public void can_eager_fetch_using_fetching_strategy()
        {
            using (var testData = new NHTestData(OrdersDomainFactory.OpenSession()))
            {
                Locator.Stub(x => x
                    .GetAllInstances<IFetchingStrategy<Customer, NHRepositoryQueryTests>>())
                    .Return(new[] {new FakeFetchingStrategy()});

                Customer customer = null;
                testData.Batch(x =>
                {
                    var products = x.CreateProducts(3);
                    var order = x.CreateOrderForProducts(products);
                    customer = x.CreateCustomer();
                    order.Customer = customer;
                });

                Customer savedCustomer = null;
                using (var scope = new UnitOfWorkScope())
                {
                    savedCustomer = new NHRepository<Customer>()
                        .For<NHRepositoryQueryTests>()
                        .Where(x => x.CustomerID == customer.CustomerID)
                        .First();
                    scope.Commit();
                }

                Assert.That(savedCustomer, Is.Not.Null);
                Assert.That(NHibernateUtil.IsInitialized(savedCustomer.Orders));
                savedCustomer.Orders.ForEach(order =>
                {
                    Assert.That(NHibernateUtil.IsInitialized(order.Customer));
                    Assert.That(NHibernateUtil.IsInitialized(order.Items));
                });
            }
        }
        
        [Test]
        public void can_query_multiple_databases()
        {
            using (var ordersTestData = new NHTestData(OrdersDomainFactory.OpenSession()))
            using (var hrTestData = new NHTestData(HRDomainFactory.OpenSession()))
            {
                Customer customer = null;
                SalesPerson salesPerson = null;
                ordersTestData.Batch(x => customer = x.CreateCustomer());
                hrTestData.Batch(x => salesPerson = x.CreateSalesPerson());

                //NOTE: This will enlist a Distributed DTC tx.
                using (var scope = new UnitOfWorkScope())
                {
                    var savedCustomer = new NHRepository<Customer>()
                        .Where(x => x.CustomerID == customer.CustomerID)
                        .First();

                    var savedPerson = new NHRepository<SalesPerson>()
                        .Where(x => x.Id == salesPerson.Id)
                        .First();

                    Assert.That(savedCustomer, Is.Not.Null);
                    Assert.That(savedPerson, Is.Not.Null);
                    scope.Commit();
                }
            }
        }
    }
}