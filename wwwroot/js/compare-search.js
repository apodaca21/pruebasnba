document.addEventListener('DOMContentLoaded', function() {
    const player1Input = document.querySelector('input[name="player1"]');
    const player2Input = document.querySelector('input[name="player2"]');
    const suggestions1 = document.getElementById('suggestions1');
    const suggestions2 = document.getElementById('suggestions2');

    if (!player1Input || !player2Input || !suggestions1 || !suggestions2) {
        return;
    }

    // autocompletado
    setupAutocomplete(player1Input, suggestions1, 'player1');
    setupAutocomplete(player2Input, suggestions2, 'player2');

    function setupAutocomplete(input, suggestionsContainer, playerField) {
        let debounceTimer;
        let currentSuggestions = [];

      
        input.addEventListener('input', function() {
            const query = this.value.trim();
            
            clearTimeout(debounceTimer);
            
            if (query.length < 2) {
                hideSuggestions(suggestionsContainer);
                return;
            }

            debounceTimer = setTimeout(() => {
                searchPlayers(query, suggestionsContainer, input);
            }, 300);
        });

    
        input.addEventListener('blur', function() {
            setTimeout(() => {
                hideSuggestions(suggestionsContainer);
            }, 200);
        });

        suggestionsContainer.addEventListener('click', function(e) {
            const suggestionItem = e.target.closest('.suggestion-item');
            if (suggestionItem) {
                const playerName = suggestionItem.dataset.playerName;
                input.value = playerName;
                hideSuggestions(suggestionsContainer);
                
       
                const otherInput = playerField === 'player1' ? player2Input : player1Input;
                if (otherInput.value.trim()) {
                    input.form.submit();
                }
            }
        });

        // teclas del teclado
        input.addEventListener('keydown', function(e) {
            const suggestions = suggestionsContainer.querySelectorAll('.suggestion-item');
            const activeSuggestion = suggestionsContainer.querySelector('.suggestion-item.active');
            
            if (suggestions.length === 0) {
                return;
            }

            switch(e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    if (activeSuggestion) {
                        activeSuggestion.classList.remove('active');
                        const next = activeSuggestion.nextElementSibling;
                        if (next) {
                            next.classList.add('active');
                        } else {
                            suggestions[0].classList.add('active');
                        }
                    } else {
                        suggestions[0].classList.add('active');
                    }
                    break;
                    
                case 'ArrowUp':
                    e.preventDefault();
                    if (activeSuggestion) {
                        activeSuggestion.classList.remove('active');
                        const prev = activeSuggestion.previousElementSibling;
                        if (prev) {
                            prev.classList.add('active');
                        } else {
                            suggestions[suggestions.length - 1].classList.add('active');
                        }
                    } else {
                        suggestions[suggestions.length - 1].classList.add('active');
                    }
                    break;
                    
                case 'Enter':
                    e.preventDefault();
                    if (activeSuggestion) {
                        const playerName = activeSuggestion.dataset.playerName;
                        input.value = playerName;
                        hideSuggestions(suggestionsContainer);
                        
                        // submit si ambos campos estan llenos
                        const otherInput = playerField === 'player1' ? player2Input : player1Input;
                        if (otherInput.value.trim()) {
                            input.form.submit();
                        }
                    }
                    break;
                    
                case 'Escape':
                    hideSuggestions(suggestionsContainer);
                    input.blur();
                    break;
            }
        });
    }

    // crear jugadiores
    async function searchPlayers(query, suggestionsContainer, input) {
        try {
            const response = await fetch(`/api/player/search?query=${encodeURIComponent(query)}`);
            if (!response.ok) {
                throw new Error('Error en la búsqueda');
            }
            
            const suggestions = await response.json();
            displaySuggestions(suggestions, suggestionsContainer);
        } catch (error) {
            console.error('Error buscando jugadores:', error);
            hideSuggestions(suggestionsContainer);
        }
    }

    //  mostrar sugerencias en bsqueda  
    function displaySuggestions(suggestions, suggestionsContainer) {
        if (suggestions.length === 0) {
            hideSuggestions(suggestionsContainer);
            return;
        }

        suggestionsContainer.innerHTML = '';
        
        suggestions.forEach(player => {
            const suggestionItem = document.createElement('div');
            suggestionItem.className = 'suggestion-item';
            suggestionItem.dataset.playerName = player.fullName;
            
            suggestionItem.innerHTML = `
                <span class="player-name">${escapeHtml(player.fullName)}</span>
                <span class="player-team">${escapeHtml(player.team)} • ${escapeHtml(player.position)}</span>
            `;
            
            suggestionsContainer.appendChild(suggestionItem);
        });

        suggestionsContainer.style.display = 'block';
    }

    // ocultar sugerencias
    function hideSuggestions(suggestionsContainer) {
        suggestionsContainer.style.display = 'none';
        suggestionsContainer.innerHTML = '';
    }

    // escapar html
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
});
