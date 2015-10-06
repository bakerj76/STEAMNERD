Module.Name = "Store"
Module.Description = "Buy things."


def ViewItems(callback, args):
    Say("The store doesn't have any items!")

Module.AddCommand("items", "See what items the store has.", ViewItems)
Module.AddCommand("buy", "Buy an item.", None)
