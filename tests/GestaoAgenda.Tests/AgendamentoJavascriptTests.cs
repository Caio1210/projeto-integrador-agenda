using Jint;
using Xunit;

namespace GestaoAgenda.Tests;

public class AgendamentoJavascriptTests
{
    private const string Dom = """
        function elemento(extra = {}) {
            return Object.assign({ listeners: {}, addEventListener(tipo, f) { this.listeners[tipo] = f; } }, extra);
        }
        function Option(texto, valor) {
            this.textContent = texto; this.value = valor; this.dataset = {};
            this.cloneNode = function() {
                const copia = new Option(this.textContent, this.value);
                copia.dataset = { ...this.dataset };
                return copia;
            };
        }
        const servico = elemento({ value: "1" });
        const selecionada = new Option("Ana", "1");
        selecionada.dataset = { preco: "99,00", duracao: "120" };
        const profissional = elemento({
            value: "1", disabled: false, options: [new Option("Selecione", ""), selecionada],

            replaceChildren(...opcoes) { this.options = opcoes; this.value = ""; },
            add(opcao) { this.options.push(opcao); }
        });
        Object.defineProperty(profissional, "selectedOptions", {
            get() { return this.options.filter(o => o.value === this.value); }
        });
        const data = elemento({ value: "" });
        const hora = elemento({ value: "" });
        const preco = { textContent: "" }, duracao = { textContent: "" }, termino = { textContent: "" };
        const aviso = { textContent: "" }, erro = { textContent: "" };
        const salvar = { disabled: bloqueadoInicial };
        const form = { querySelector() { return salvar; } };
        const raiz = {
            dataset: {
                url: "/agendamento/profissionais", servicoOriginal: historicoInicial ? "1" : "0",
                ofertaOriginal: historicoInicial ? "1" : "", precoOriginal: "40,00", duracaoOriginal: "60"
            },
            closest() { return form; },
            querySelector(seletor) {
                return {
                    "[data-servico]": servico, "[data-profissional]": profissional,
                    "[data-data]": data, "[data-hora]": hora, "[data-resumo-preco]": preco,
                    "[data-resumo-duracao]": duracao, "[data-resumo-termino]": termino,
                    "[data-aviso-historico]": aviso, "[data-erro-ofertas]": erro
                }[seletor];
            }
        };
        const document = { querySelector() { return raiz; }, baseURI: "http://teste/" };
        class AbortController {
            constructor() { this.signal = { aborted: false }; }
            abort() { this.signal.aborted = true; }
        }
        class URL {
            constructor(url, base) {
                this.url = url; this.params = {};
                this.searchParams = { set: (nome, valor) => { this.params[nome] = valor; } };
            }
        }
        const pedidos = [];
        function fetch(url, options) {
            return new Promise((resolve, reject) => pedidos.push({ url, options, resolve, reject }));
        }
        function responder(indice, ofertas, ok = true) {
            pedidos[indice].resolve({ ok, json() { return Promise.resolve(ofertas); } });
        }
        """;

    private static Engine Executar(bool historico = false, bool bloqueado = false)
    {
        var engine = new Engine(options => options.TimeoutInterval(TimeSpan.FromSeconds(3)));
        engine.SetValue("historicoInicial", historico);
        engine.SetValue("bloqueadoInicial", bloqueado);
        engine.Execute(Dom);
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/GestaoAgenda/wwwroot/js/agendamento-ofertas.js"));
        engine.Execute(File.ReadAllText(path));
        return engine;
    }

    [Fact]
    public void ResumoPreservaValoresReservadosEmVezDosAtuais()
    {
        using var engine = Executar(historico: true);
        Assert.Equal("R$ 40,00", engine.Evaluate("preco.textContent").AsString());
        Assert.Equal("60 min", engine.Evaluate("duracao.textContent").AsString());
    }

    [Fact]
    public void TrocaDeProfissionalRecalculaResumoETerminoAtravessandoMeiaNoite()
    {
        using var engine = Executar(historico: true);
        engine.Execute("""
            Date.prototype.toLocaleString = function() { return this.toISOString(); };
            const bia = new Option("Bia", "2"); bia.dataset = { preco: "80,00", duracao: "60" };
            profissional.add(bia); profissional.value = "2";
            data.value = "2030-01-07"; hora.value = "23:30:00";
            profissional.listeners.change();
            """);
        Assert.Equal("R$ 80,00", engine.Evaluate("preco.textContent").AsString());
        Assert.Contains("2030-01-08T00:30:00", engine.Evaluate("termino.textContent").AsString());
    }

    [Fact]
    public async Task RespostasForaDeOrdemNaoSubstituemServicoMaisRecente()
    {
        using var engine = Executar();
        engine.Execute("""
            var primeira = servico.listeners.change();
            servico.value = "2";
            var segunda = servico.listeners.change();
            responder(1, [{ id: 20, nome: "Mais recente", preco: "20,00", duracaoMinutos: 20 }]);
            """);
        await engine.EvaluateAsync("segunda");
        engine.Execute("responder(0, [{ id: 10, nome: 'Antiga', preco: '10,00', duracaoMinutos: 10 }]);");
        await engine.EvaluateAsync("primeira");
        Assert.True(engine.Evaluate("pedidos[0].options.signal.aborted").AsBoolean());
        Assert.True(engine.Evaluate("profissional.options.some(o => o.value === '20')").AsBoolean());
        Assert.False(engine.Evaluate("profissional.options.some(o => o.value === '10')").AsBoolean());
    }

    [Fact]
    public async Task LimparServicoDuranteBuscaNaoDeixaProfissionalDesabilitado()
    {
        using var engine = Executar();
        engine.Execute("var busca = servico.listeners.change(); servico.value = ''; var limpa = servico.listeners.change();");
        await engine.EvaluateAsync("limpa");
        engine.Execute("responder(0, []);");
        await engine.EvaluateAsync("busca");
        Assert.False(engine.Evaluate("profissional.disabled").AsBoolean());
        Assert.False(engine.Evaluate("salvar.disabled").AsBoolean());
        Assert.Equal("Não informado", engine.Evaluate("preco.textContent").AsString());
    }

    [Fact]
    public async Task FalhaDeBuscaBloqueiaEnvioEOrientaFallback()
    {
        using var engine = Executar();
        engine.Execute("var busca = servico.listeners.change(); responder(0, [], false);");
        await engine.EvaluateAsync("busca");
        Assert.True(engine.Evaluate("salvar.disabled").AsBoolean());
        Assert.False(engine.Evaluate("profissional.disabled").AsBoolean());
        Assert.Contains("Carregar profissionais", engine.Evaluate("erro.textContent").AsString());
    }

    [Fact]
    public async Task SemElegiveisExibeMensagemSemOferecerProfissionaisInativos()
    {
        using var engine = Executar();
        engine.Execute("var busca = servico.listeners.change(); responder(0, []);");
        await engine.EvaluateAsync("busca");
        Assert.Equal(1, engine.Evaluate("profissional.options.length").AsNumber());
        Assert.Contains("Nenhum profissional ativo", engine.Evaluate("erro.textContent").AsString());
    }

    [Fact]
    public async Task OpcaoHistoricaVoltaMarcadaComoInativaSemMudarSnapshots()
    {
        using var engine = Executar(historico: true);
        engine.Execute("var busca = servico.listeners.change(); responder(0, []);");
        await engine.EvaluateAsync("busca");
        engine.Execute("profissional.value = '1'; profissional.listeners.change();");
        Assert.Contains("histórica/inativa", engine.Evaluate("profissional.selectedOptions[0].textContent").AsString());
        Assert.Contains("Oferta histórica/inativa", engine.Evaluate("aviso.textContent").AsString());
        Assert.Equal("R$ 40,00", engine.Evaluate("preco.textContent").AsString());
    }

    [Fact]
    public async Task BuscaBemSucedidaNaoDesbloqueiaFormularioQueFalhouNoServidor()
    {
        using var engine = Executar(bloqueado: true);
        engine.Execute("var busca = servico.listeners.change(); responder(0, []);");
        await engine.EvaluateAsync("busca");
        Assert.True(engine.Evaluate("salvar.disabled").AsBoolean());
    }
}