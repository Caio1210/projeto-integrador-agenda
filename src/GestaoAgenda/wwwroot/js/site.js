

let table = new DataTable('#js-table', {
    language: {
        url: '//cdn.datatables.net/plug-ins/2.3.7/i18n/pt-BR.json',
    },
    order: false,
    scrollX: true,
    responsive: true,
    pageLength: 25,
    layout: {
        topStart: {
            buttons: [{
                extend: 'excelHtml5',
                text: "Excel",
                className: "btn-excel",
            }],
        }
    },
    initComplete: function () {
        $(".dt-container label").addClass("form-label fs-14");
        $(".dt-container input, .dt-container select").addClass("border-gray3 form-control");

        $(".dt-layout-table .dt-layout-cell").addClass("border-0");
        $(".dt-search label").html('<i class="bi bi-search"></i>');
        $(".dt-search input").attr("placeholder", "Pesquisar");
    }
});

$('.money').mask('000.000.000.000.000,00', { reverse: true });
$('.money2').mask("#.##0,00", { reverse: true });