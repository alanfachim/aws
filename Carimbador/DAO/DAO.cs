using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Carimbador
{
    public class DAO
    {
        private Amazon.DynamoDBv2.AmazonDynamoDBClient DynamoDb;
        private AmazonDynamoDBStreamsClient DynamoDbStream;
        private string stream;
        private GetRecordsRequest recordsRequest;

        private IDatabase RedisR;
        private IDatabase RedisW; 
        private List<Dictionary<string, AttributeValue>> tbgrupo = new List<Dictionary<string, AttributeValue>>();
        private List<Dictionary<string, AttributeValue>> tbregra = new List<Dictionary<string, AttributeValue>>();
        private List<Dictionary<string, AttributeValue>> tbdecomposicao = new List<Dictionary<string, AttributeValue>>();
         
        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {

            var it = DynamoDbStream.GetRecordsAsync(recordsRequest).Result;
            if (it.Records.Count > 0)
            {
                foreach (var record in it.Records)
                {
                    switch (record.EventName)
                    {
                        case "INSERT":
                            Console.WriteLine(record);
                            break;
                        case "DELETE":
                            Console.WriteLine(record);
                            break;
                    }
                    
                } 
            }
            if (it.NextShardIterator != null)
            {
                recordsRequest.ShardIterator = it.NextShardIterator;
            }
            else
            {
                recordsRequest.ShardIterator = GetLastShard("arn:aws:dynamodb:sa-east-1:469122751664:table/regra/stream/2021-05-24T14:05:13.672");
            }

        }
        public DAO()
        {
            var URIRedis = Environment.GetEnvironmentVariable("URIRedis");
            DynamoDb = new AmazonDynamoDBClient();
             
            //////////////////////////////////////////// Implementar tabela única///////////////////////////////////////////////////////////////
            //Carrega no cache
            var result = DynamoDb.ScanAsync("grupo", new Dictionary<string, Amazon.DynamoDBv2.Model.Condition>(), default).Result;
            HPCsharp.Algorithm.Quicksort<Dictionary<string, AttributeValue>>(result.Items.ToArray(), 0, result.Items.Count, new CompararGrupo());
            tbgrupo = result.Items;

            //Carrega no cache
            result = DynamoDb.ScanAsync("regra", new Dictionary<string, Amazon.DynamoDBv2.Model.Condition>(), default).Result;
            HPCsharp.Algorithm.Quicksort<Dictionary<string, AttributeValue>>(result.Items.ToArray(), 0, result.Items.Count, new CompararRegra());
            tbregra = result.Items;

            //Carrega no cache
            result = DynamoDb.ScanAsync("decomposicao", new Dictionary<string, Amazon.DynamoDBv2.Model.Condition>(), default).Result; 
            tbdecomposicao = result.Items;
            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            DynamoDbStream = new AmazonDynamoDBStreamsClient();
            stream = GetLastShard("arn:aws:dynamodb:sa-east-1:469122751664:table/regra/stream/2021-05-24T14:05:13.672");
            recordsRequest = new GetRecordsRequest()
            {
                ShardIterator = stream
            };

            var aTimer = new System.Timers.Timer();
            aTimer.Interval = 60000;
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;

            ConnectionMultiplexer RedisClusterR = ConnectionMultiplexer.Connect(URIRedis + ":6379");
            RedisR = RedisClusterR.GetDatabase();
            ConnectionMultiplexer RedisClusterW = ConnectionMultiplexer.Connect(URIRedis + ":6379");
            RedisW = RedisClusterW.GetDatabase(); 

        }

        private string GetLastShard(string StreamArn)
        {
            DescribeStreamRequest streamRequest = new DescribeStreamRequest()
            {
                StreamArn = StreamArn
            };
            var resp = DynamoDbStream.DescribeStreamAsync(streamRequest).Result;
            var shards = resp.StreamDescription.Shards.ToArray();
            var lastone = shards[shards.Length - 1];

            GetShardIteratorRequest shardIteratorRequest = new GetShardIteratorRequest()
            {
                StreamArn = StreamArn,
                ShardIteratorType = "LATEST",
                ShardId = lastone.ShardId
            };
            return DynamoDbStream.GetShardIteratorAsync(shardIteratorRequest).Result.ShardIterator;
        }

        public DAO(AmazonDynamoDBClient DynamoDb, IDatabase RedisR, IDatabase RedisW)
        {
            this.DynamoDb = DynamoDb;
            this.RedisR = RedisR;
            this.RedisW = RedisW; 
        }

        public Dictionary<string, AttributeValue> binarySerach(List<Dictionary<string, AttributeValue>> array, dynamic match, string key)
        {
            int xii = 0, len = array.Count;
            while (len > xii)
            {
                dynamic value = array[(int)((len - xii) / 2) + xii][key].S;
                if (value == match)
                {
                    return array[(int)((len - xii) / 2)];
                }
                if (string.Compare(value, match) < 0)
                {
                    xii = (int)((len - xii) / 2) + xii;
                }
                else
                {
                    len = (int)((len - xii) / 2);
                }
            }
            return null;
        }

        public List<Dictionary<string, AttributeValue>> binarySerachRange(List<Dictionary<string, AttributeValue>> array, dynamic match, string key)
        {
            int xi = 0, xf = 0, xii = 0, len = array.Count;
            List<Dictionary<string, AttributeValue>> range = new List<Dictionary<string, AttributeValue>>();
            while (len > xii)
            {
                dynamic value = array[(int)((len - xii) / 2) + xii][key].S;
                if (value == match)
                {
                    xi = xf = (int)((len - xii) / 2) + xii;
                    while ((dynamic)array[xi][key].S == (dynamic)array[xf][key].S && xi > 0)
                    {
                        xi--;
                    }
                    if (xi > 0)
                        xi++;
                    while ((dynamic)array[xi][key].S == (dynamic)array[xf][key].S && xf < array.Count - 1)
                    {
                        xf++;
                    }
                    if ((dynamic)array[xi][key].S != (dynamic)array[xf][key].S)
                        xf--;
                    for (int count = xi; count <= xf; count++)
                    {
                        range.Add(array[count]);
                    }
                    return range;
                }
                if (string.Compare(value, match) < 0)
                {
                    xii = (int)((len - xii) / 2) + xii;
                }
                else
                {
                    len = (int)((len - xii) / 2);
                }
            }
            return range;
        }

        public class CompararGrupo : IComparer<Dictionary<string, AttributeValue>>
        {
            public int Compare(Dictionary<string, AttributeValue> first, Dictionary<string, AttributeValue> second)
            {
                return string.Compare(first["cod_produto"].ToString(), second["cod_produto"].ToString());
            }
        }
        public class CompararRegra : IComparer<Dictionary<string, AttributeValue>>
        {
            public int Compare(Dictionary<string, AttributeValue> first, Dictionary<string, AttributeValue> second)
            {
                return string.Compare(first["chave"].ToString(), second["chave"].ToString());
            }
        }
        public async Task<Dictionary<string, object>> buscaRegras(string chave, object input)
        {
            var regra = binarySerachRange(tbregra, chave, "chave");
            return await Task.FromResult(new Dictionary<string, object>() { { "regra", regra }, { "input", input } });
        }
        public async Task<Dictionary<string, object>> getGrupParameters(string cod_produto, object input)
        { 
            var grupo = binarySerach(tbgrupo,  cod_produto, "cod_produto");
            return await Task.FromResult(new Dictionary<string, object>() { { "grupo", grupo }, { "input", input } }); 
        }
        public async Task<Dictionary<string, object>> getDecomposicao(string chave_regra, object input)
        { 
            var grupo = binarySerach(tbdecomposicao, chave_regra, "chave");
            return await Task.FromResult(new Dictionary<string, object>() { { "grupo", grupo }, { "input", input } });
        }

        public async Task<Dictionary<string, object>> buscaPontoCurva(string cod_curva, string dat_execucao, string vencimento, object input)
        {

            var redisItem = RedisR.StringGet(cod_curva + dat_execucao.ToString());
            if (redisItem.HasValue)
            {
                var result = Document.FromJson(redisItem);
                var a = result["pontos"].AsDocument()[vencimento].AsDouble();
                return await Task.FromResult(new Dictionary<string, object>() { { "curva", a }, { "input", input } });
            }
            else
            {
                Dictionary<string, Amazon.DynamoDBv2.Model.Condition> keyConditions = new Dictionary<string, Amazon.DynamoDBv2.Model.Condition>
                {
                    {
                        "cod_curva",
                        new Amazon.DynamoDBv2.Model.Condition
                        {
                        ComparisonOperator = "EQ",
                        AttributeValueList = new List<AttributeValue> { new AttributeValue { S = cod_curva  }  }
                        }
                    },
                    {
                        "database",
                        new Amazon.DynamoDBv2.Model.Condition
                        {
                        ComparisonOperator = "EQ",
                        AttributeValueList = new List<AttributeValue> { new AttributeValue { S = dat_execucao.ToString() }  }
                        }
                    }
                };
                QueryRequest request = new QueryRequest
                {
                    TableName = "curva",
                    KeyConditions = keyConditions
                };
                var result = await DynamoDb.QueryAsync(request, default);
                var items = result.Items.FirstOrDefault();
                var doc = Document.FromAttributeMap(items);
                RedisW.StringSet(cod_curva + dat_execucao.ToString(), doc.ToJson());
                var a = doc["pontos"].AsDocument()[vencimento].AsDouble();
                return await Task.FromResult(new Dictionary<string, object>() { { "curva", a }, { "input", input } });
            }
        }


    }
}
