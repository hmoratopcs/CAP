﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;

namespace Sample.RabbitMQ.SqlServer.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly ICapPublisher _capBus;

        public ValuesController(ICapPublisher capPublisher)
        {
            _capBus = capPublisher;
        }

        [Route("~/without/transaction")]
        public async Task<IActionResult> WithoutTransaction()
        {
            await _capBus.PublishAsync("sample.rabbitmq.sqlserver", DateTime.Now);

            return Ok();
        }

        [Route("~/adonet/transaction")]
        public IActionResult AdonetWithTransaction()
        {
            using (var connection = new SqlConnection(AppDbContext.ConnectionString))
            {
                using (var transaction = connection.BeginTransaction(_capBus, autoCommit: false))
                {
                    //your business code
                    connection.Execute("insert into test(name) values('test')", transaction: transaction);

                    for (int i = 0; i < 5; i++)
                    {
                        _capBus.Publish("sample.rabbitmq.sqlserver", DateTime.Now);
                    }

                    transaction.Commit();
                }
            }

            return Ok();
        }

        [Route("~/ef/transaction")]
        public IActionResult EntityFrameworkWithTransaction([FromServices]AppDbContext dbContext)
        {
            using (var trans = dbContext.Database.BeginTransaction(_capBus, autoCommit: false))
            {
                dbContext.Persons.Add(new Person() { Name = "ef.transaction" });

                for (int i = 0; i < 5; i++)
                {
                    _capBus.Publish("sample.rabbitmq.sqlserver", DateTime.Now);
                }

                dbContext.SaveChanges();

                trans.Commit();
            }
            return Ok();
        }

        [NonAction]
        [CapSubscribe("#.rabbitmq.sqlserver")]
        public void Subscriber(DateTime time)
        {
            Console.WriteLine($@"{DateTime.Now}, Subscriber invoked, Sent time:{time}");
        }
    }
}
