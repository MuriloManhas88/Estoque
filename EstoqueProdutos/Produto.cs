using System;

namespace EstoqueTeste
{
    public class Produto
    {
        public DateTime Data { get; set; }
        public string NomeProd { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double Custo { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public double CustoMedio { get; set; }
        public int Saldo { get; set; }
    }
}
