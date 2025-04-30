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
        private static List<(string nomeArquivo, List<Produto> produtos)> tabelas = new();
        private static readonly string[] expectedHeaders = { "data", "produto", "quantidade", "custo", "tipo", "custo medio", "saldo" };
        private static Dictionary<string, (double custoMedio, int saldo, bool primeiraEntrada)> controleProdutos = new();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            bool sair = false;

            while (!sair)
            {
                Console.Clear();
                Console.WriteLine("╔════════════════════════════╗");
                Console.WriteLine("║     GESTÃO DE ESTOQUE      ║");
                Console.WriteLine("╠════════════════════════════╣");
                Console.WriteLine("║ 1. Importar planilha CSV   ║");
                Console.WriteLine("║ 2. Ver tabela de produtos  ║");
                Console.WriteLine("║ 3. Filtrar por intervalo   ║");
                Console.WriteLine("║ 4. Sair                    ║");
                Console.WriteLine("╚════════════════════════════╝");
                Console.Write("Escolha uma opção: ");

                switch (Console.ReadLine())
                {
                    case "1":
                        ImportarCsv();
                        break;
                    case "2":
                        SelecionarTabelaParaExibir(false);
                        break;
                    case "3":
                        SelecionarTabelaParaExibir(true);
                        break;
                    case "4":
                        sair = true;
                        break;
                    default:
                        EscreverErro("\nOpção inválida. Pressione ENTER para tentar novamente.");
                        Console.ReadLine();
                        break;
                }
            }
             
            Console.WriteLine("\nPrograma finalizado. Pressione ENTER para sair.");
            Console.ReadLine();
        }

        private static void ImportarCsv()
        {
            try
            {
                Console.Write("\nDigite o nome do arquivo CSV (ex: estoque.csv): ");
                var nomeArquivo = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(nomeArquivo))
                    throw new InvalidOperationException("Nome de arquivo inválido.");

                var caminhoArquivo = Path.Combine("../../../", nomeArquivo);

                if (!File.Exists(caminhoArquivo))
                    throw new FileNotFoundException("Arquivo CSV não encontrado.");

                var config = new CsvConfiguration(new CultureInfo("pt-BR"))
                {
                    HasHeaderRecord = true,
                    Delimiter = ";",
                    PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
                };

                List<Produto> produtosImportados;

                using (var reader = new StreamReader(caminhoArquivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                using (var csv = new CsvReader(reader, config))
                {
                    csv.Read();
                    csv.ReadHeader();

                    ValidarEstrutura(csv.HeaderRecord);

                    csv.Context.RegisterClassMap<ProdutoMap>();
                    produtosImportados = csv.GetRecords<Produto>().ToList();
                }

                ValidarSaldo(produtosImportados);

                tabelas.Add((nomeArquivo, produtosImportados));

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nArquivo importado com sucesso! Pressione ENTER para continuar.");
                Console.ResetColor();
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                EscreverErro($"\n[Erro]: {ex.Message}");
                Console.ReadLine();
            }
        }

        private static void ValidarEstrutura(string[] headers)
        {
            var normalizedHeaders = headers
                .Select(h => h?.Trim().ToLowerInvariant().Replace("\uFEFF", "").Replace("\u200B", ""))
                .ToArray();

            var missingHeaders = expectedHeaders.Where(h => !normalizedHeaders.Contains(h)).ToList();
            var isOrderCorrect = normalizedHeaders.SequenceEqual(expectedHeaders);

            if (missingHeaders.Any())
                throw new InvalidOperationException("Não foi possível inserir seu arquivo (faltando colunas).");

            if (!isOrderCorrect)
                throw new InvalidOperationException("Não foi possível inserir seu arquivo (colunas fora de ordem).");
        }

        private static void ValidarSaldo(List<Produto> produtos)
        {
            var controleLocal = new Dictionary<string, (int saldo, bool primeiraEntrada)>();

            foreach (var produto in produtos.OrderBy(p => p.Data))
            {
                var nomeProduto = produto.NomeProd.Trim();

                if (!controleLocal.ContainsKey(nomeProduto))
                    controleLocal[nomeProduto] = (0, true);

                var (saldo, primeiraEntrada) = controleLocal[nomeProduto];

                if (produto.Tipo.ToLowerInvariant() == "e")
                {
                    saldo += produto.Quantidade;
                    primeiraEntrada = false;
                }
                else if (produto.Tipo.ToLowerInvariant() == "s")
                {
                    if (saldo < produto.Quantidade)
                        throw new InvalidOperationException($"Erro: Saldo insuficiente para saída de {produto.Quantidade} unidades do produto {nomeProduto}. Saldo atual: {saldo}");
                    saldo -= produto.Quantidade;
                }

                controleLocal[nomeProduto] = (saldo, primeiraEntrada);
            }
        }

        private static void SelecionarTabelaParaExibir(bool aplicarFiltro)
        {
            if (tabelas.Count == 0)
            {
                EscreverErro("\nNenhuma tabela carregada. Importe uma planilha primeiro.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("\nTabelas disponíveis:");
            for (int i = 0; i < tabelas.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {tabelas[i].nomeArquivo}");
            }

            Console.Write("\nDigite o número da tabela que deseja visualizar: ");
            if (int.TryParse(Console.ReadLine(), out int escolha) && escolha >= 1 && escolha <= tabelas.Count)
            {
                var tabelaSelecionada = tabelas[escolha - 1].produtos;

                if (aplicarFiltro)
                    FiltrarPorIntervaloDeDatas(tabelaSelecionada);
                else
                    ExibirTabela(tabelaSelecionada);
            }
            else
            {
                EscreverErro("\nEscolha inválida. Pressione ENTER para continuar.");
                Console.ReadLine();
            }
        }

        private static void ExibirTabela(List<Produto> lista)
        {
            if (lista == null || lista.Count == 0)
            {
                EscreverErro("\nNenhum produto para exibir.");
                Console.ReadLine();
                return;
            }

            controleProdutos.Clear();

            Console.WriteLine("+------------+------------+-----+--------+------+--------------+-------+");
            Console.WriteLine($"| {Centralizar("Data", 10)} | {Centralizar("Produto", 10)} | {Centralizar("Qtd", 3)} | {Centralizar("Custo", 6)} | {Centralizar("Tipo", 4)} | {Centralizar("Custo Médio", 12)} | {Centralizar("Saldo", 5)} |");
            Console.WriteLine("+------------+------------+-----+--------+------+--------------+-------+");

            foreach (var produto in lista.OrderBy(p => p.Data))
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
                    saldo -= produto.Quantidade;
                }

                controleProdutos[nomeProduto] = (custoMedio, saldo, primeiraEntrada);

                Console.WriteLine($"| {Centralizar(produto.Data.ToString("dd/MM/yyyy"), 10)} | {Centralizar(nomeProduto, 10)} | {Centralizar(produto.Quantidade.ToString(), 3)} | {Centralizar(produto.Custo.ToString("N2"), 6)} | {Centralizar(produto.Tipo, 4)} | {Centralizar(Math.Round(custoMedio, 3).ToString("N3"), 12)} | {Centralizar(saldo.ToString(), 5)} |");
            }

            Console.WriteLine("+------------+------------+-----+--------+------+--------------+-------+");
            Console.WriteLine("\nPressione ENTER para continuar.");
            Console.ReadLine();
        }

        private static void FiltrarPorIntervaloDeDatas(List<Produto> lista)
        {
            if (lista == null || lista.Count == 0)
            {
                EscreverErro("\nNenhum produto encontrado.");
                Console.ReadLine();
                return;
            }

            var datasDisponiveis = lista
                .Select(p => p.Data.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            Console.WriteLine("\nDatas disponíveis nesta tabela:");
            foreach (var data in datasDisponiveis)
            {
                Console.WriteLine($"- {data:dd/MM/yyyy}");
            }

            Console.WriteLine();

            var dataInicial = LerData("Digite a data inicial (dd/MM/yyyy): ");
            var dataFinal = LerData("Digite a data final (dd/MM/yyyy): ");

            if (dataFinal < dataInicial)
            {
                var temp = dataFinal;
                dataFinal = dataInicial;
                dataInicial = temp;
            }

            var produtosFiltrados = lista
                .Where(p => p.Data.Date >= dataInicial.Date && p.Data.Date <= dataFinal.Date)
                .OrderBy(p => p.Data)
                .ToList();

            if (!produtosFiltrados.Any())
            {
                EscreverErro("\nNenhum produto encontrado nesse intervalo.");
                Console.ReadLine();
                return;
            }

            ExibirTabela(produtosFiltrados);
        }

        private static DateTime LerData(string mensagem)
        {
            while (true)
            {
                Console.Write(mensagem);
                var entrada = Console.ReadLine();

                if (DateTime.TryParseExact(entrada, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                    return data;

                EscreverErro("Formato inválido. Tente novamente.");
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

        private static void EscreverErro(string mensagem)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(mensagem);
            Console.ResetColor();
        }
    }
}
