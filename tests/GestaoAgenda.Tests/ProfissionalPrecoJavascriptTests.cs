using GestaoAgenda.ViewModels;
using Jint;
using Xunit;

namespace GestaoAgenda.Tests;

public class ProfissionalPrecoJavascriptTests
{
    private const string Dom = """
        function elemento(extra = {}) {
            return Object.assign({
                listeners: {},
                addEventListener(tipo, callback) { this.listeners[tipo] = callback; }
            }, extra);
        }
        const selecionado = elemento({ checked: selecionadoInicial });
        const campos = elemento({ disabled: false });
        const preco = elemento({
            value: precoInicial, validacao: "",
            setCustomValidity(mensagem) { this.validacao = mensagem; }
        });
        const duracao = elemento({ value: "30" });
        const ativo = elemento({ checked: true });
        const erro = { textContent: "" };
        const status = { textContent: "", className: "" };
        const botao = { disabled: false, textContent: "Salvar" };
        const linha = elemento({
            querySelector(seletor) {
                return {
                    "[data-selecionado]": selecionado, "[data-oferta-campos]": campos,
                    "[data-preco]": preco, "[data-duracao]": duracao,
                    "[data-oferta-ativo]": ativo, "[data-preco-erro]": erro,
                    "[data-status-oferta]": status
                }[seletor];
            }
        });
        const form = elemento({
            querySelectorAll() { return [linha]; },
            querySelector() { return botao; },
            reportValidity() { return preco.validacao === ""; },
            setAttribute() {}
        });
        const document = { querySelector() { return form; } };
        const window = { addEventListener() {} };
        """;

    private static Engine Executar(string valor, bool selecionado = true)
    {
        var caminho = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/GestaoAgenda/wwwroot/js/profissional-servicos.js"));
        var engine = new Engine(options => options.TimeoutInterval(TimeSpan.FromSeconds(3)));
        engine.SetValue("precoInicial", valor);
        engine.SetValue("selecionadoInicial", selecionado);
        engine.Execute(Dom);
        engine.Execute(File.ReadAllText(caminho));
        return engine;
    }

    [Theory]
    [InlineData("40,00", true)]
    [InlineData("40,5", true)]
    [InlineData("40", true)]
    [InlineData("0,01", true)]
    [InlineData("1.234,56", true)]
    [InlineData("9.999.999.999.999.999,99", true)]
    [InlineData("40.00", false)]
    [InlineData("0,00", false)]
    [InlineData("-40,00", false)]
    [InlineData("1.234", false)]
    [InlineData("1,234", false)]
    [InlineData("12345.678,90", false)]
    [InlineData("1a234,56", false)]
    [InlineData("", false)]
    public void ScriptRealValidaPrecoComoServidor(string texto, bool esperado)
    {
        using var engine = Executar(texto);
        Assert.Equal(esperado, engine.Evaluate("preco.validacao === ''").AsBoolean());
        Assert.Equal(esperado, PrecoPtBr.TryParse(texto, out _));
    }

    [Fact]
    public void CorrigirValorLimpaMensagemECustomValidity()
    {
        using var engine = Executar("texto");
        Assert.False(engine.Evaluate("preco.validacao === ''").AsBoolean());
        engine.Execute("preco.value = '40,00'; preco.listeners.input();");
        Assert.True(engine.Evaluate("preco.validacao === '' && erro.textContent === ''").AsBoolean());
    }

    [Fact]
    public void OfertaNaoSelecionadaNaoBloqueiaESelecionarValidaOValor()
    {
        using var engine = Executar("40,00", selecionado: false);
        Assert.True(engine.Evaluate("campos.disabled && preco.validacao === ''").AsBoolean());
        engine.Execute("selecionado.checked = true; linha.listeners.change();");
        Assert.True(engine.Evaluate("!campos.disabled && preco.required && preco.validacao === ''").AsBoolean());
    }

    [Fact]
    public void PrecoValidoPermiteEnvioEImpedeSegundoClique()
    {
        using var engine = Executar("40,00");
        engine.Execute("""
            const evento = { defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
            form.listeners.submit(evento);
            """);
        Assert.True(engine.Evaluate("!evento.defaultPrevented && botao.disabled").AsBoolean());
        engine.Execute("form.listeners.submit(evento);");
        Assert.True(engine.Evaluate("evento.defaultPrevented").AsBoolean());
    }
}