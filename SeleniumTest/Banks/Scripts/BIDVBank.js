function selectUserAccount(account) {
    var options = $('.x-combo-list-inner').find('div');
    for (i = 0; i < options.length; i++) {
        var item = options[i];
        if ($(item).text() == account) {
            $(item).click();
        }
    }
}

function getUserAccounts() {
    var items = [];
    var options = $('.x-combo-list-inner').find('div');
    for (i = 0; i < options.length; i++) {
        if (i != 0) items.push("(VND) - " + $(options[i]).text()+ " - ");
    }
    return items;
}

function test() {
    return "test";
}