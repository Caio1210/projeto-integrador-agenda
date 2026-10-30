(() => {
    "use strict";
    const raiz = document.querySelector("[data-agendamento-ofertas]");
    if (!raiz) return;
    const servico = raiz.querySelector("[data-servico]");
    const profissional = raiz.querySelector("[data-profissional]");
    const data = raiz.querySelector("[data-data]");
    const hora = raiz.querySelector("[data-hora]");
    const preco = raiz.querySelector("[data-resumo-preco]");
    const duracao = raiz.querySelector("[data-resumo-duracao]");
    const termino = raiz.querySelector("[data-resumo-termino]");
    const aviso = raiz.querySelector("[data-aviso-historico]");
    const erro = raiz.querySelector("[data-erro-ofertas]");
    const salvar = raiz.closest("form").querySelector("[data-salvar-agendamento]");
    const podeSalvar = !salvar.disabled;
    const original = raiz.dataset.ofertaOriginal
        ? [...profissional.options].find(o => o.value === raiz.dataset.ofertaOriginal)?.cloneNode(true) : null;
    let requisicao;
    let versao = 0;

    function resumo() {
        const opcao = profissional.selectedOptions[0];
        const manter = servico.value === raiz.dataset.servicoOriginal &&
            profissional.value !== "" && profissional.value === raiz.dataset.ofertaOriginal;
        const valor = manter ? raiz.dataset.precoOriginal : opcao?.dataset.preco;
        const minutos = manter ? raiz.dataset.duracaoOriginal : opcao?.dataset.duracao;
        preco.textContent = valor ? "R$ " + valor : "Não informado";
        duracao.textContent = minutos ? minutos + " min" : "Não informada";
        termino.textContent = "Não informado";
        aviso.textContent = opcao?.dataset.historica === "true"
            ? "Oferta histórica/inativa: para reagendar ou reativar, selecione uma oferta ativa." : "";
        if (data.value && hora.value && minutos) {
            // UTC é usado apenas para aritmética de calendário, sem conversão de fuso.
            const fim = new Date(data.value + "T" + hora.value + "Z");
            fim.setUTCMinutes(fim.getUTCMinutes() + Number(minutos));
            if (!Number.isNaN(fim.getTime()) && fim.getUTCFullYear() <= 9999)
                termino.textContent = fim.toLocaleString("pt-BR", {
                    timeZone: "UTC", year: "numeric", month: "2-digit", day: "2-digit",
                    hour: "2-digit", minute: "2-digit", second: "2-digit"
                });
        }
    }

    servico.addEventListener("change", async () => {
        requisicao?.abort();
        requisicao = new AbortController();
        const atual = ++versao;
        profissional.replaceChildren(new Option("Selecione um profissional", ""));
        erro.textContent = "";
        resumo();
        if (!servico.value) {
            profissional.disabled = false;
            salvar.disabled = !podeSalvar;
            return;
        }
        profissional.disabled = true;
        salvar.disabled = true;
        erro.textContent = "Carregando profissionais…";
        try {
            const url = new URL(raiz.dataset.url, document.baseURI);
            url.searchParams.set("servicoId", servico.value);
            const resposta = await fetch(url, { signal: requisicao.signal, headers: { Accept: "application/json" } });
            if (!resposta.ok) throw new Error("Falha de carregamento");
            const ofertas = await resposta.json();
            if (atual !== versao) return;
            for (const oferta of ofertas) {
                const opcao = new Option(oferta.nome, String(oferta.id));
                opcao.dataset.preco = oferta.preco;
                opcao.dataset.duracao = String(oferta.duracaoMinutos);
                profissional.add(opcao);
            }
            if (original && servico.value === raiz.dataset.servicoOriginal &&
                !ofertas.some(o => String(o.id) === original.value))
            {
                const historica = original.cloneNode(true);
                if (historica.dataset.historica !== "true")
                    historica.textContent += " (oferta histórica/inativa)";
                historica.dataset.historica = "true";
                profissional.add(historica);
            }
            erro.textContent = ofertas.length ? "" : "Nenhum profissional ativo oferece este serviço.";
            salvar.disabled = !podeSalvar;
        } catch (falha) {
            if (falha.name !== "AbortError" && atual === versao)
                erro.textContent = "Não foi possível carregar os profissionais. Use o botão Carregar profissionais para tentar novamente.";
        } finally {
            if (atual === versao) { profissional.disabled = false; resumo(); }
        }
    });
    profissional.addEventListener("change", resumo);
    data.addEventListener("change", resumo);
    hora.addEventListener("input", resumo);
    resumo();
})();