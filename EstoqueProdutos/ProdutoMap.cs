using CsvHelper.Configuration;

namespace EstoqueTeste
{
    public class ProdutoMap : ClassMap<Produto>
    {
        public ProdutoMap()
        {
            Map(p => p.Data).Name("data");
            Map(p => p.NomeProd).Name("produto");
            Map(p => p.Quantidade).Name("quantidade");
            Map(p => p.Custo).Name("custo");
            Map(p => p.Tipo).Name("tipo");
            Map(p => p.CustoMedio).Name("custo medio");
            Map(p => p.Saldo).Name("saldo");
        }
    }
}
