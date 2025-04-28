using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using EstoqueTeste;
using Xunit;

namespace EstoqueTeste.UnitTests
{
    public class CsvImportTests
    {
        private CsvConfiguration GetConfig()
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ";",
                PrepareHeaderForMatch = args => args.Header.Trim().ToLower(),
                MissingFieldFound = null
            };
        }

        [Fact]
        public void Deve_Processar_Arquivo_CSV_Com_Estrutura_Correta()
        {
            // Arrange
            var csvContent =
@"Data;Produto;Quantidade;Custo;Tipo
01/04/2025;Maçã;20;5;e";

            var filePath = Path.GetTempFileName();
            File.WriteAllText(filePath, csvContent);

            // Act
            List<Produto> produtos;
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, GetConfig()))
            {
                csv.Context.RegisterClassMap<ProdutoMap>();
                produtos = csv.GetRecords<Produto>().ToList();
            }

            // Assert
            Assert.Single(produtos);
            Assert.Equal("Maçã", produtos[0].NomeProd);
            Assert.Equal(20, produtos[0].Quantidade);
            Assert.Equal(5, produtos[0].Custo);

            File.Delete(filePath);
        }

        [Fact]
        public void Deve_Verificar_Colunas_Obrigatorias_No_CSV()
        {
            // Arrange
            var expectedHeaders = new[] { "data", "produto", "quantidade", "custo", "tipo" };

            var csvContent =
@"Data;Produto;Quantidade;Custo;Tipo
01/04/2025;Maçã;20;5;e";

            var filePath = Path.GetTempFileName();
            File.WriteAllText(filePath, csvContent);

            // Act
            string[] actualHeaders;
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, GetConfig()))
            {
                csv.Read();
                csv.ReadHeader();
                actualHeaders = csv.HeaderRecord.Select(h => h.Trim().ToLower()).ToArray();
            }

            // Assert
            Assert.True(expectedHeaders.All(header => actualHeaders.Contains(header)));

            File.Delete(filePath);
        }

        [Fact]
        public void Deve_Calcular_CustoMedio_E_Saldo_Corretamente()
        {
            // Arrange
            var produtos = new List<Produto>
            {
                new Produto { Quantidade = 20, Custo = 5, Tipo = "e" },
                new Produto { Quantidade = 15, Custo = 6, Tipo = "s" },
                new Produto { Quantidade = 30, Custo = 4.9, Tipo = "e" },
                new Produto { Quantidade = 50, Custo = 5.1, Tipo = "e" },
            };

            double custoMedio = 0;
            int saldo = 0;
            bool primeiraEntrada = true;

            foreach (var produto in produtos)
            {
                if (produto.Tipo.ToLower() == "e")
                {
                    if (primeiraEntrada)
                    {
                        custoMedio = produto.Custo;
                        saldo += produto.Quantidade;
                        primeiraEntrada = false;
                    }
                    else
                    {
                        double totalCusto = custoMedio * saldo;
                        totalCusto += produto.Custo * produto.Quantidade;
                        saldo += produto.Quantidade;
                        custoMedio = saldo > 0 ? totalCusto / saldo : 0;
                    }
                }
                else if (produto.Tipo.ToLower() == "s")
                {
                    saldo -= produto.Quantidade;
                }
            }

            // Assert
            Assert.Equal(5.02, Math.Round(custoMedio, 2));
            Assert.Equal(85, saldo);
        }

        [Fact]
        public void Saida_Nao_Deve_Alterar_CustoMedio()
        {
            // Arrange
            var produtos = new List<Produto>
            {
                new Produto { Quantidade = 20, Custo = 5, Tipo = "e" },
                new Produto { Quantidade = 10, Custo = 6, Tipo = "s" },
            };

            double custoMedio = 0;
            int saldo = 0;
            bool primeiraEntrada = true;

            foreach (var produto in produtos)
            {
                if (produto.Tipo.ToLower() == "e")
                {
                    if (primeiraEntrada)
                    {
                        custoMedio = produto.Custo;
                        saldo += produto.Quantidade;
                        primeiraEntrada = false;
                    }
                    else
                    {
                        double totalCusto = custoMedio * saldo;
                        totalCusto += produto.Custo * produto.Quantidade;
                        saldo += produto.Quantidade;
                        custoMedio = saldo > 0 ? totalCusto / saldo : 0;
                    }
                }
                else if (produto.Tipo.ToLower() == "s")
                {
                    saldo -= produto.Quantidade;
                }
            }

            // Assert
            Assert.Equal(5, Math.Round(custoMedio, 2));
            Assert.Equal(10, saldo);
        }

        [Fact]
        public void Deve_Lancar_Erro_Se_Saida_Maior_Que_Saldo()
        {
            // Arrange
            var saldoAtual = 10;
            var quantidadeSaida = 15;

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                if (quantidadeSaida > saldoAtual)
                    throw new InvalidOperationException("Saldo Insuficiente");
            });

            Assert.Equal("Saldo Insuficiente", ex.Message);
        }

        [Fact]
        public void Saldo_Deve_Ser_Somatorio_Entradas_Menos_Saidas()
        {
            // Arrange
            var produtos = new List<Produto>
            {
                new Produto { Quantidade = 50, Custo = 5, Tipo = "e" },
                new Produto { Quantidade = 20, Custo = 5, Tipo = "s" },
                new Produto { Quantidade = 30, Custo = 6, Tipo = "e" },
                new Produto { Quantidade = 10, Custo = 5, Tipo = "s" },
            };

            int saldo = 0;

            foreach (var produto in produtos)
            {
                if (produto.Tipo.ToLower() == "e")
                    saldo += produto.Quantidade;
                else if (produto.Tipo.ToLower() == "s")
                    saldo -= produto.Quantidade;
            }

            // Assert
            Assert.Equal(50, saldo);
        }

        [Fact]
        public void Deve_Recalcular_CustoMedio_E_Saldo_A_Cada_Linha()
        {
            // Arrange
            var produtos = new List<Produto>
            {
                new Produto { Quantidade = 10, Custo = 5, Tipo = "e" },
                new Produto { Quantidade = 20, Custo = 6, Tipo = "e" },
                new Produto { Quantidade = 5, Custo = 5, Tipo = "s" },
                new Produto { Quantidade = 15, Custo = 7, Tipo = "e" },
            };

            double custoMedio = 0;
            int saldo = 0;
            bool primeiraEntrada = true;
            var custoMediosCalculados = new List<double>();
            var saldosCalculados = new List<int>();

            // Act
            foreach (var produto in produtos)
            {
                if (produto.Tipo.ToLower() == "e")
                {
                    if (primeiraEntrada)
                    {
                        custoMedio = produto.Custo;
                        saldo += produto.Quantidade;
                        primeiraEntrada = false;
                    }
                    else
                    {
                        double totalCusto = custoMedio * saldo;
                        totalCusto += produto.Custo * produto.Quantidade;
                        saldo += produto.Quantidade;
                        custoMedio = saldo > 0 ? totalCusto / saldo : 0;
                    }
                }
                else if (produto.Tipo.ToLower() == "s")
                {
                    saldo -= produto.Quantidade;
                }

                custoMediosCalculados.Add(Math.Round(custoMedio, 2));
                saldosCalculados.Add(saldo);
            }

            // Assert
            Assert.Equal(new List<double> { 5.00, 5.67, 5.67, 6.17 }, custoMediosCalculados);
            Assert.Equal(new List<int> { 10, 30, 25, 40 }, saldosCalculados);
        }

        private List<Produto> GerarProdutosFake()
        {
            return new List<Produto>
            {
                new Produto { Data = new DateTime(2025, 4, 2), NomeProd = "Banana", Quantidade = 50, Custo = 3.0, Tipo = "e" },
                new Produto { Data = new DateTime(2025, 4, 3), NomeProd = "Banana", Quantidade = 20, Custo = 3.5, Tipo = "s" },
                new Produto { Data = new DateTime(2025, 4, 5), NomeProd = "Banana", Quantidade = 30, Custo = 2.8, Tipo = "e" }
            };
        }

        [Fact]
        public void Deve_Filtrar_Produtos_Entre_Duas_Datas()
        {
            var produtos = GerarProdutosFake();
            var dataInicial = new DateTime(2025, 4, 2);
            var dataFinal = new DateTime(2025, 4, 3);

            var filtrados = produtos
                .Where(p => p.Data.Date >= dataInicial && p.Data.Date <= dataFinal)
                .OrderBy(p => p.Data)
                .ToList();

            Assert.Equal(2, filtrados.Count);
        }

        [Theory]
        [InlineData("sim")]
        [InlineData("nao")]
        public void Deve_Aceitar_Resposta_Valida(string resposta)
        {
            var respostaTratada = resposta.Trim().ToLower();
            Assert.True(respostaTratada == "sim" || respostaTratada == "nao");
        }

        [Theory]
        [InlineData("talvez")]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("Sim!")]
        public void Deve_Lancar_Excecao_Resposta_Invalida(string resposta)
        {
            var respostaTratada = resposta.Trim().ToLower();
            Assert.False(respostaTratada == "sim" || respostaTratada == "nao");
        }

        [Fact]
        public void Deve_Mostrar_Todos_Produtos_Quando_Resposta_Nao()
        {
            var produtos = GerarProdutosFake();
            var resultado = produtos.OrderBy(p => p.Data).ToList();
            Assert.Equal(3, resultado.Count);
        }

        [Fact]
        public void Deve_Mostrar_Erro_Se_Colunas_Estiverem_Em_Ordem_Incorreta()
        {
            var csvContent = @"Produto;Data;Quantidade;Tipo;Custo
2025-04-28;Banana;100;e;3.0";

            var expectedMessage = "Não foi possível inserir seu arquivo";

            var config = GetConfig();

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            try
            {
                using var reader = new StringReader(csvContent);
                using var csv = new CsvReader(reader, config);

                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord.Select(h => h.Trim().ToLower()).ToArray();

                var expectedOrder = new[] { "data", "produto", "quantidade", "custo", "tipo" };

                if (!headers.SequenceEqual(expectedOrder))
                {
                    Console.WriteLine(expectedMessage);
                    return;
                }

                csv.Context.RegisterClassMap<ProdutoMap>();
                var produtos = csv.GetRecords<Produto>().ToList();
            }
            catch
            {
                // Ignora
            }

            var output = consoleOutput.ToString().Trim();
            Assert.Contains(expectedMessage, output);
        }

        [Fact]
        public void Deve_Mostrar_Erro_Se_Coluna_Obrigatoria_Estiver_Faltando()
        {
            var csvContent = @"data;produto;quantidade;tipo
2025-04-28;Banana;100;e";

            var expectedMessage = "Não foi possível inserir seu arquivo";

            var config = GetConfig();

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            try
            {
                using var reader = new StringReader(csvContent);
                using var csv = new CsvReader(reader, config);

                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord.Select(h => h.Trim().ToLower()).ToArray();

                var requiredHeaders = new[] { "data", "produto", "quantidade", "custo", "tipo" };

                if (!requiredHeaders.All(h => headers.Contains(h)))
                {
                    Console.WriteLine(expectedMessage);
                    return;
                }

                csv.Context.RegisterClassMap<ProdutoMap>();
                var produtos = csv.GetRecords<Produto>().ToList();
            }
            catch
            {
                // Ignora
            }

            var output = consoleOutput.ToString().Trim();
            Assert.Contains(expectedMessage, output);
        }
    }
}
