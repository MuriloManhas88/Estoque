using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace EstoqueTeste
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new CsvConfiguration(new CultureInfo("pt-BR"))
            {
                HasHeaderRecord = true,
                Delimiter = ";",
                PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            };

            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.Write("Digite o nome do arquivo CSV (ex: estoque.csv): ");
                var nomeArquivo = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(nomeArquivo))
                    throw new InvalidOperationException("Nome de arquivo inválido.");

                var caminhoArquivo = Path.Combine("../../../", nomeArquivo);

                if (!File.Exists(caminhoArquivo))
                    throw new FileNotFoundException("Arquivo CSV não encontrado.");

                List<Produto> produtos;

                using (var reader = new StreamReader(caminhoArquivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                using (var csv = new CsvReader(reader, config))
                {
                    csv.Read();
                    csv.ReadHeader();

                    var headers = csv.HeaderRecord
                        .Select(h => h?.Trim().ToLowerInvariant().Replace("\uFEFF", "").Replace("\u200B", ""))
                        .ToArray();

                    var expectedHeaders = new[] { "data", "produto", "quantidade", "custo", "tipo", "custo medio", "saldo" };

                    var missingHeaders = expectedHeaders.Where(h => !headers.Contains(h)).ToList();
                    var isOrderCorrect = headers.SequenceEqual(expectedHeaders);

                    if (missingHeaders.Any())
                        throw new InvalidOperationException("Não foi possível inserir seu arquivo (faltando colunas).");

                    if (!isOrderCorrect)
                        throw new InvalidOperationException("Não foi possível inserir seu arquivo (colunas fora de ordem).");

                    csv.Context.RegisterClassMap<ProdutoMap>();
                    produtos = csv.GetRecords<Produto>().ToList();
                }

                if (produtos.Count == 0)
                {
                    Console.WriteLine("\nNenhum produto encontrado no arquivo.");
                    return;
                }

                var datasDisponiveis = produtos
                    .Select(p => p.Data.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                Console.WriteLine("\nDatas disponíveis no estoque:");
                foreach (var data in datasDisponiveis)
                {
                    Console.WriteLine($"- {data:dd/MM/yyyy}");
                }

                List<Produto> produtosFiltrados;
                string resposta;
                while (true)
                {
                    Console.Write("\nDeseja informar um intervalo de datas? (sim/nao): ");
                    resposta = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";

                    if (resposta == "sim" || resposta == "nao")
                        break;

                    Console.WriteLine("Resposta inválida. Digite apenas 'sim' ou 'nao'.");
                }

                if (resposta == "sim")
                {
                    var dataInicial = LerData("Digite a data inicial (formato: dd/MM/yyyy): ");
                    var dataFinal = LerData("Digite a data final (formato: dd/MM/yyyy): ");

                    if (dataFinal < dataInicial)
                    {
                        var temp = dataFinal;
                        dataFinal = dataInicial;
                        dataInicial = temp;
                    }

                    produtosFiltrados = produtos
                        .Where(p => p.Data.Date >= dataInicial.Date && p.Data.Date <= dataFinal.Date)
                        .OrderBy(p => p.Data)
                        .ToList();
                }
                else
                {
                    produtosFiltrados = produtos.OrderBy(p => p.Data).ToList();
                }

                if (produtosFiltrados.Count == 0)
                {
                    Console.WriteLine("\nNenhum produto encontrado para o filtro aplicado.");
                    return;
                }

                // Cabeçalho da tabela
                Console.WriteLine("+------------+------------+-----+--------+------+--------------+-------+");
                Console.WriteLine($"| {Centralizar("Data", 10)} | {Centralizar("Produto", 10)} | {Centralizar("Qtd", 3)} | {Centralizar("Custo", 6)} | {Centralizar("Tipo", 4)} | {Centralizar("Custo Médio", 12)} | {Centralizar("Saldo", 5)} |");
                Console.WriteLine("+------------+------------+-----+--------+------+--------------+-------+");

                var controleProdutos = new Dictionary<string, (double custoMedio, int saldo, bool primeiraEntrada)>();

                foreach (var produto in produtosFiltrados)
                {
                    var nomeProduto = produto.NomeProd.Trim();

                    if (!controleProdutos.ContainsKey(nomeProduto))
                        controleProdutos[nomeProduto] = (0, 0, true);

                    var (custoMedio, saldo, primeiraEntrada) = controleProdutos[nomeProduto];

                    if (produto.Tipo.ToLowerInvariant() == "e")
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
                    else if (produto.Tipo.ToLowerInvariant() == "s")
                    {
                        if (produto.Quantidade > saldo)
                        {
                            throw new InvalidOperationException($"Erro: Saldo insuficiente para saída de {produto.Quantidade} unidades do produto {nomeProduto}. Saldo atual: {saldo}");
                        }

                        saldo -= produto.Quantidade;
                    }

                    controleProdutos[nomeProduto] = (custoMedio, saldo, primeiraEntrada);

                    Console.WriteLine($"| {Centralizar(produto.Data.ToString("dd/MM/yyyy"), 10)} | {Centralizar(nomeProduto, 10)} | {Centralizar(produto.Quantidade.ToString(), 3)} | {Centralizar(produto.Custo.ToString("N2"), 6)} | {Centralizar(produto.Tipo, 4)} | {Centralizar(Math.Round(custoMedio, 3).ToString("N3"), 12)} | {Centralizar(saldo.ToString(), 5)} |");
                }

                Console.WriteLine("+------------+------------+-----+--------+------+--------------+-------+");

                Console.WriteLine("\nResumo Final:");
                foreach (var item in controleProdutos)
                {
                    var nomeProduto = item.Key;
                    var (custoMedioFinal, saldoFinal, _) = item.Value;
                    Console.WriteLine($"Produto: {nomeProduto} | Saldo Final: {saldoFinal} | Custo Médio Final: {Math.Round(custoMedioFinal, 3)}");
                }
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\n[Erro]: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Erro inesperado]: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Detalhes: {ex.InnerException.Message}");
            }
        }

        private static DateTime LerData(string mensagem)
        {
            while (true)
            {
                Console.Write(mensagem);
                var entrada = Console.ReadLine();

                if (DateTime.TryParseExact(entrada, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                {
                    return data;
                }

                Console.WriteLine("Formato inválido. Tente novamente.");
            }
        }

        private static string Centralizar(string texto, int largura)
        {
            if (string.IsNullOrEmpty(texto)) texto = "";
            if (texto.Length >= largura) return texto.Substring(0, largura);

            int espacos = largura - texto.Length;
            int padLeft = espacos / 2 + texto.Length;

            return texto.PadLeft(padLeft).PadRight(largura);
        }
    }
}
