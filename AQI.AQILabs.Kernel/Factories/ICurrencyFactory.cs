﻿/*
 * The MIT License (MIT)
 * Copyright (c) 2007-2019, Arturo Rodriguez All rights reserved.
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace AQI.AQILabs.Kernel.Factories
{
    public interface ICurrencyFactory
    {
        Currency CreateCurrency(string name, string description, Calendar calendar);

        Currency FindCurrency(string name);
        Currency FindCurrency(int id);

        List<Currency> Currencies();
        List<string> CurrencyNames();

        void SetProperty(Currency currency, string name, object value);
    }

    public interface ICurrencyPairFactory
    {
        CurrencyPair CreateCurrencyPair(Currency buy, Currency sell, Instrument fxInstrument);
        CurrencyPair FindCurrencyPair(Currency buy, Currency sell);
        CurrencyPair FindCurrencyPair(Instrument FXInstrument);

        void SetProperty(CurrencyPair currencyPair, string name, object value);
    }
}
