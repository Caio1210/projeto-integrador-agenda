(() => {
    "use strict";
    const form = document.querySelector("[data-profissional-form]");
    if (!form) return;

    function precoValido(texto) {
        const valor = texto.trim();
        if (!valor || valor.length > 24 ||
            !/^(?:[0-9]+(?:,[0-9]{1,2})?|[1-9][0-9]{0,2}(?:[.][0-9]{3})+,[0-9]{2})$/.test(valor)) return false;
        const partes = valor.replace(/[.]/g, "").split(",");
        const inteiros = partes[0].replace(/^0+/, "");
        return inteiros.length <= 16 && /[1-9]/.test(partes.join(""));
    }

    for (const linha of form.querySelectorAll("[data-oferta]")) {
        const selecionado = linha.querySelector("[data-selecionado]");
        const campos = linha.querySelector("[data-oferta-campos]");
        const preco = linha.querySelector("[data-preco]");
        const duracao = linha.querySelector("[data-duracao]");
        const ativo = linha.querySelector("[data-oferta-ativo]");
        const erro = linha.querySelector("[data-preco-erro]");
        const status = linha.querySelector("[data-status-oferta]");
        const atualizar = () => {
            campos.disabled = !selecionado.checked;
            preco.required = selecionado.checked;
            duracao.required = selecionado.checked;
            const mensagem = selecionado.checked && !precoValido(preco.value)
                ? "Informe um preço válido em reais. Exemplo: 40,00" : "";
            preco.setCustomValidity(mensagem);
            erro.textContent = mensagem;
            status.textContent = !selecionado.checked ? "Não selecionado" : (ativo.checked ? "Ativo" : "Inativo");
            status.className = "badge mt-3 " + (selecionado.checked && ativo.checked ? "bg-success" : "bg-secondary");
        };
        linha.addEventListener("change", atualizar);
        preco.addEventListener("input", atualizar);
        atualizar();
    }

    let enviando = false;
    form.addEventListener("submit", event => {
        if (enviando) { event.preventDefault(); return; }
        if (event.defaultPrevented || !form.reportValidity() ||
            (window.jQuery?.validator && !window.jQuery(form).valid())) {
            event.preventDefault();
            return;
        }
        enviando = true;
        const botao = form.querySelector("[data-salvar]");
        botao.disabled = true;
        botao.textContent = "Salvando…";
        form.setAttribute("aria-busy", "true");
    });
    // Restaurar o botão ao voltar pelo histórico do navegador.
    window.addEventListener("pageshow", event => {
        if (event.persisted && enviando) window.location.reload();
    });
})();