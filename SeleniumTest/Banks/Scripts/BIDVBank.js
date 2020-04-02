function selectUserAccount(account) {
    var options = $('.x-combo-list-inner').find('div');
    for (i = 0; i < options.length; i++) {
        var item = options[i];
        if ($(item).text() == account) {
            $(item).click();
        }
    }
}