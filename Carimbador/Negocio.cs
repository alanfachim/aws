using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Carimbador
{
    public class Negocio
    { 
        private DAO db;
        private ProducerConfig produtorConfig = new ProducerConfig
        {
            BootstrapServers = "b-1.clusterwithallproperti.0dwwzo.c4.kafka.sa-east-1.amazonaws.com:9096,b-2.clusterwithallproperti.0dwwzo.c4.kafka.sa-east-1.amazonaws.com:9096",
            //SslCaLocation = "/Path-to/cluster-ca-certificate.pem",
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.ScramSha512,
            SaslUsername = "alan",
            SaslPassword = "fachim" 
        };
        private ConsumerConfig  consumidorConfig = new ConsumerConfig
        {
            BootstrapServers = "b-1.clusterwithallproperti.0dwwzo.c4.kafka.sa-east-1.amazonaws.com:9096,b-2.clusterwithallproperti.0dwwzo.c4.kafka.sa-east-1.amazonaws.com:9096",
            //SslCaLocation = "/Path-to/cluster-ca-certificate.pem",
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.ScramSha512,
            SaslUsername = "alan",
            SaslPassword = "fachim",
            GroupId = "dotnet-example-group-1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.RoundRobin
        };
        public IProducer<string, string> produtor;
        public IConsumer<string, string> consumidor;
        public Negocio()
        {
            db = new DAO();
            produtor = new ProducerBuilder<string, string>(produtorConfig).Build();
            consumidor = new ConsumerBuilder<string, string>(consumidorConfig).Build();
        }
        public Negocio(DAO _db)
        {
            db = _db;
        }
        public class CompararRegra : IComparer<Dictionary<string, AttributeValue>>
        {
            public int Compare(Dictionary<string, AttributeValue> first, Dictionary<string, AttributeValue> second)
            {
                return string.Compare(first["inicio"].ToString(), second["inicio"].ToString());
            }
        } 
        public void Processar()
        {    
            var id = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            List<Task<string>> taskMessages = new List<Task<string>>();
            using (produtor)
            {
                using (consumidor)
                {
                    consumidor.Subscribe("consumer-topic");
                    Console.WriteLine("ok " + id.ToString());
                    int i = 0;
                    var log = ""; 
                    while (true)
                    {
                        var response = consumidor.Consume(5000); //consumo Kfka
                        var i0 = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                        var time1 = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - i0;
                        var inicio = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

                        if (response != null)
                        { 
                            var message = response.Message;
                            Dictionary<string, object> input;

                            try
                            {
                                input = JsonConvert.DeserializeObject<Dictionary<string, object>>(message.Value);
                                input.Add("ReceiptHandle", message.Key);
                                input.Add("startup", inicio);
                                Task<string> task = db.getGrupParameters(input["cod_produto"].ToString(), input).ContinueWith((rt1) =>
                                  {
                                      var value = (Dictionary<string, object>)rt1.Result;
                                      var grupo = (Dictionary<string, AttributeValue>)value["grupo"];
                                      var input = (Dictionary<string, object>)value["input"];

                                      var chave_regra = grupo["grupo"].S.ToString() + ';';
                                      foreach (var parametro in grupo["parametros"].L)
                                      {
                                          var p = ((JObject)input["parametros"])[parametro.S];
                                          if (p != null)
                                          {
                                              chave_regra += parametro.S + '=' + p["value"] + ';';
                                          }
                                      }
                                      return db.buscaRegras(chave_regra, input).Result;
                                  }).ContinueWith((rt2) =>
                                  {
                                      var value = rt2.Result;
                                      var regras = (List<Dictionary<string, AttributeValue>>)value["regra"];
                                      var input = (Dictionary<string, object>)value["input"];
                                      HPCsharp.Algorithm.Quicksort<Dictionary<string, AttributeValue>>(regras.ToArray(), 0, regras.Count, new CompararRegra());
                                      for (var r = 0; r < regras.Count; r++)
                                      {
                                          if (int.Parse(regras[r]["inicio"].S.Split(';')[0]) <= int.Parse(input["database"].ToString()) &&
                                          int.Parse(input["carencia"].ToString()) > int.Parse(regras[r]["inicio"].S.Split(';')[1]) && int.Parse(input["carencia"].ToString()) < int.Parse(regras[r]["fim"].N))
                                          {
                                              List<Task<Dictionary<string, object>>> taskList = new List<Task<Dictionary<string, object>>>();
                                              taskList.Add(db.buscaPontoCurva(regras[r]["curva_transfer"].S, input["database"].ToString(), input["vencimento"].ToString(), input));
                                              taskList.Add(db.buscaPontoCurva(regras[r]["curva_custo"].S, input["database"].ToString(), input["vencimento"].ToString(), input));
                                              //buscar decomposicao  
                                              return Task.WhenAll(taskList.ToArray()).Result;
                                          }
                                      }
                                      return null;
                                  }).ContinueWith((rt2) =>
                                  {
                                      var value = rt2.Result;
                                      var input = ((Dictionary<string, object>)value[0]["input"])["startup"].ToString();
                                      var r = produtor.ProduceAsync("producer-topic", new Message<string, string> { Value = JsonConvert.SerializeObject(value) }).Result;
                                      return ((Dictionary<string, object>)value[0]["input"])["ReceiptHandle"].ToString();
                                  });
                                taskMessages.Add(task);
                            }
                            catch (Exception error)
                            {
                                Console.WriteLine("erro:" + i);
                            }
                            if (taskMessages.Count > 5)
                            {
                                var retorno = Task.WhenAll(taskMessages.ToArray()).ContinueWith((rt2) =>
                                  {
                                      Console.WriteLine(JsonConvert.SerializeObject(rt2.Result)); 
                                  });
                                retorno.Wait();
                                taskMessages.Clear();
                                taskMessages = new List<Task<string>>();

                            }

                        }
                    }
                }
            }
        }


    }
}
